using System.Net;
using System.Text;

namespace ByteBait.Api.Tests;

// Plays the API for tests of the Blazor client's plain C#: answers the requests in order, one answer per request
internal sealed class FakeApi(params Func<HttpResponseMessage>[] answers) : HttpMessageHandler
{
    private int next;

    public HttpClient Client => new(this, disposeHandler: false) { BaseAddress = new Uri("https://localhost/") };

    public static Func<HttpResponseMessage> Answer(HttpStatusCode status, string? json = null) => () =>
        new HttpResponseMessage(status)
        {
            Content = json is null ? null : new StringContent(json, Encoding.UTF8, "application/json"),
        };

    // What HttpClient throws when its Timeout runs out
    public static HttpResponseMessage TimesOut() => throw new TaskCanceledException("The request timed out.");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(answers[next++]());
}
