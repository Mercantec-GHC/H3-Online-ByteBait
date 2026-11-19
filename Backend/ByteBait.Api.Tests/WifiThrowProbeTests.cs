using System.Net;
using System.Net.ServerSentEvents;
using System.Text;
using Microsoft.AspNetCore.Hosting;

namespace ByteBait.Api.Tests;

// Exercise the actual HTTP and event-stream boundaries with a temporary listener.
public class WifiThrowProbeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Page = "/api/test/wifi-throw";

    [Fact]
    public async Task Staging_routes_each_protocol_line_to_its_listener_with_the_correct_source()
    {
        using var host = factory.WithWebHostBuilder(b => b.UseEnvironment("Staging"));
        using var client = host.CreateClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        var endpoint = Page + "/" + Guid.NewGuid();
        Assert.Contains("Send simulated throw", await client.GetStringAsync(Page, ct));
        using var response = await client.GetAsync(endpoint + "/events", HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var events = SseParser.Create(stream).EnumerateAsync(ct).GetAsyncEnumerator();
        Assert.True(await events.MoveNextAsync());
        Assert.Equal("ready", events.Current.EventType);

        using var missing = await client.PostAsync(Page + "/" + Guid.NewGuid(), new StringContent("THROW:1\n"), ct);
        Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);

        foreach (var (body, simulated) in new[] { ("THROW:72\n", true), ("THROW:0\n", false), ("THROW:100\r\n", false) })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new StringContent(body) };
            if (simulated)
                request.Headers.Add("X-Probe-Source", "simulated");
            using var receipt = await client.SendAsync(request, ct);
            Assert.Equal(HttpStatusCode.NoContent, receipt.StatusCode);
            Assert.True(await events.MoveNextAsync());
            Assert.Equal(simulated ? "simulated-throw" : "throw", events.Current.EventType);
            Assert.Equal(body.TrimEnd('\r', '\n'), events.Current.Data);
        }
    }

    [Theory]
    [InlineData("THROW:101\n", "text/plain", HttpStatusCode.BadRequest)]
    [InlineData("THROW:-1\n", "text/plain", HttpStatusCode.BadRequest)]
    [InlineData("THROW:4", "text/plain", HttpStatusCode.BadRequest)]
    [InlineData("THROW:4\nTHROW:5\n", "text/plain", HttpStatusCode.BadRequest)]
    [InlineData("THROW:4\n", "application/json", HttpStatusCode.UnsupportedMediaType)]
    public async Task Invalid_messages_are_rejected(string body, string mediaType, HttpStatusCode expected)
    {
        using var host = factory.WithWebHostBuilder(b => b.UseEnvironment("Staging"));
        using var client = host.CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, mediaType);
        using var response = await client.PostAsync(Page + "/" + Guid.NewGuid(), content);
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Production_returns_404_for_all_probe_routes()
    {
        using var host = factory.WithWebHostBuilder(b => b.UseEnvironment("Production"));
        using var client = host.CreateClient();
        var endpoint = Page + "/" + Guid.NewGuid();
        using var page = await client.GetAsync(Page);
        using var events = await client.GetAsync(endpoint + "/events", HttpCompletionOption.ResponseHeadersRead);
        using var publish = await client.PostAsync(endpoint, new StringContent("THROW:1\n"));
        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, events.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, publish.StatusCode);
    }
}
