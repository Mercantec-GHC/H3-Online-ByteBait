using System.Net;

namespace ByteBait.Api.Tests;

// The API serves the Blazor client, so the game and the API run in one container.
public class BlazorHostingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/")]
    [InlineData("/game")]
    [InlineData("/cookies")]
    public async Task Pages_return_the_Blazor_client(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<div id=\"app\">", await response.Content.ReadAsStringAsync());
    }

    // Fingerprint placeholders are not replaced when the API publishes the client (dotnet/runtime#121993),
    // so index.html must load the Blazor runtime by its plain name. See ByteBait.Client.csproj.
    [Fact]
    public async Task Index_html_loads_the_Blazor_runtime_without_placeholders()
    {
        var html = await _client.GetStringAsync("/");

        Assert.Contains("<script src=\"_framework/blazor.webassembly.js\"></script>", html);
        Assert.DoesNotContain("#[", html);
    }

    [Fact]
    public async Task Unknown_api_route_returns_404_problem_details()
    {
        var response = await _client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
