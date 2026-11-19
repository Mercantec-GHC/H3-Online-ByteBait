using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Net.Http.Headers;

namespace ByteBait.Api.Diagnostics;

// Short-lived, same-origin Oplà test. Only connected browsers own a queue.
// The probe never calls the cast endpoint or writes to the database.
internal sealed class WifiThrowProbe
{
    private readonly Dictionary<Guid, Channel<SseItem<string>>> _listeners = [];
    private readonly Lock _gate = new();

    internal static void MapRoutes(IEndpointRouteBuilder endpoints)
    {
        var probe = new WifiThrowProbe();
        var group = endpoints.MapGroup("/api/test/wifi-throw");
        group.MapGet("", () => Results.Stream(
            typeof(WifiThrowProbe).Assembly.GetManifestResourceStream(typeof(WifiThrowProbe), "WifiThrow.html")!,
            "text/html; charset=utf-8"));
        group.MapGet("/{id:guid}/events", (Guid id, HttpContext context) => probe.Listen(id, context));
        group.MapPost("/{id:guid}", (Guid id, HttpRequest request, CancellationToken ct) => probe.Publish(id, request, ct));
    }

    private async Task Listen(Guid id, HttpContext context)
    {
        var channel = Channel.CreateBounded<SseItem<string>>(new BoundedChannelOptions(16) { SingleReader = true });
        IResult? error = null;
        lock (_gate)
        {
            if (_listeners.ContainsKey(id))
                error = Results.Problem(statusCode: 409, title: "This test already has a listener.");
            else if (_listeners.Count >= 32)
                error = Results.Problem(statusCode: 503, title: "All test connections are in use.");
            else
                _listeners.Add(id, channel);
        }
        if (error is not null)
        {
            await error.ExecuteAsync(context);
            return;
        }

        try
        {
            context.Response.Headers["X-Accel-Buffering"] = "no";
            await TypedResults.ServerSentEvents(Events(channel.Reader, context.RequestAborted)).ExecuteAsync(context);
        }
        finally
        {
            lock (_gate)
            {
                _listeners.Remove(id);
                channel.Writer.TryComplete();
            }
        }
    }

    private static async IAsyncEnumerable<SseItem<string>> Events(ChannelReader<SseItem<string>> reader,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Ready travels through the same stream as the throw, after registration.
        yield return new SseItem<string>("ready", "ready");
        await foreach (var message in reader.ReadAllAsync(ct))
            yield return message;
    }

    private async Task<IResult> Publish(Guid id, HttpRequest request, CancellationToken ct)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var media) ||
            !string.Equals(media.MediaType.Value, "text/plain", StringComparison.OrdinalIgnoreCase))
            return Results.Problem(statusCode: 415, title: "Send one text/plain protocol line.");

        var source = request.Headers["X-Probe-Source"];
        if (source.Count != 0 && (source.Count != 1 || source[0] != "simulated"))
            return Results.Problem(statusCode: 400, title: "Unknown probe source.");

        // 32 protocol bytes + CRLF, and one extra byte to detect an oversized/chunked body.
        var buffer = new byte[35];
        var size = await request.Body.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken: ct);
        var line = ParseThrow(buffer, size);
        if (line is null)
            return Results.Problem(statusCode: 400, title: "Expected one THROW line with strength 0 to 100.");

        var message = new SseItem<string>(line, source.Count == 0 ? "throw" : "simulated-throw");
        lock (_gate)
        {
            if (!_listeners.TryGetValue(id, out var listener))
                return Results.Problem(statusCode: 409, title: "Open the test page and wait for Ready.");
            if (!listener.Writer.TryWrite(message))
                return Results.Problem(statusCode: 503, title: "The test listener is busy.");
        }
        return Results.NoContent();
    }

    private static string? ParseThrow(byte[] buffer, int size)
    {
        if (size is 0 or > 34 || buffer[size - 1] != '\n')
            return null;
        int length = size - 1;
        if (length > 0 && buffer[length - 1] == '\r')
            length--;
        var line = Encoding.ASCII.GetString(buffer, 0, length);
        if (line.Length is < 7 or > 9 || !line.StartsWith("THROW:", StringComparison.Ordinal))
            return null;
        var digits = line.AsSpan(6);
        return digits.IndexOfAnyExceptInRange('0', '9') < 0 && int.Parse(digits) <= 100 ? line : null;
    }
}
