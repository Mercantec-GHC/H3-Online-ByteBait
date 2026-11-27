using ByteBait.Api.Hosting;

namespace ByteBait.Api.Tests;

public class CloudflareSchemeTests
{
    [Theory]
    [InlineData("""{"scheme":"https"}""", "https")]
    [InlineData("""{ "scheme": "https" }""", "https")]
    [InlineData("""{"scheme":"http"}""", "http")]
    [InlineData("""{"scheme":"HTTPS"}""", null)]
    [InlineData("""{"scheme":"ftp"}""", null)]
    [InlineData("""{"scheme":1}""", null)]
    [InlineData("""{}""", null)]
    [InlineData("""["https"]""", null)]
    [InlineData("https", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Reads_the_scheme_the_visitor_used(string? headerValue, string? expected) =>
        Assert.Equal(expected, CloudflareScheme.SchemeFrom(headerValue));
}
