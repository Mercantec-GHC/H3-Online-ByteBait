using ByteBait.Client.Accounts;

namespace ByteBait.Api.Tests;

// The client has no C# test project of its own. The API references it, so its plain C# can be tested here.
public class SafeReturnUrlTests
{
    private static readonly Uri Site = new("https://bytebait-test.mercantec.tech/");

    [Theory]
    [InlineData("/game?levelId=2", "https://bytebait-test.mercantec.tech/game?levelId=2")]
    [InlineData("/admin", "https://bytebait-test.mercantec.tech/admin")]
    [InlineData("/", "https://bytebait-test.mercantec.tech/")]
    public void Keeps_pages_on_our_site(string returnUrl, string expected) =>
        Assert.Equal(expected, SafeReturnUrl.OrHome(returnUrl, Site));

    [Theory]
    [InlineData("/\t/evil.example")]
    [InlineData("/\n/evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("\\\\evil.example")]
    [InlineData("https://evil.example")]
    [InlineData("https://bytebait-test.mercantec.tech.evil.example/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("game")]
    [InlineData("")]
    [InlineData(null)]
    public void Sends_everything_else_home(string? returnUrl) =>
        Assert.Equal("/", SafeReturnUrl.OrHome(returnUrl, Site));

    // Stays on our site even when the path itself starts with //
    [Fact]
    public void A_path_that_resolves_to_double_slash_stays_on_our_site()
    {
        var target = new Uri(SafeReturnUrl.OrHome("/.//evil.example", Site));

        Assert.Equal(Site.Host, target.Host);
    }
}
