using ByteBait.Api.Hosting;

namespace ByteBait.Api.Tests;

public class CloudflareSchemeTests
{
    [Theory]
    [InlineData("""{"scheme":"https"}""", true)]
    [InlineData("""{ "scheme": "https" }""", true)]
    [InlineData("""{"scheme":"http"}""", false)]
    [InlineData("""{"scheme":"HTTPS"}""", false)]
    [InlineData("""{}""", false)]
    [InlineData("""["https"]""", false)]
    [InlineData("https", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_a_visitor_that_used_https_counts_as_https(string? headerValue, bool expected) =>
        Assert.Equal(expected, CloudflareScheme.IsHttps(headerValue));
}
