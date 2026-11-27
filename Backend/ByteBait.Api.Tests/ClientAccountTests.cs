using System.Net;
using ByteBait.Client.Accounts;
using static ByteBait.Api.Tests.FakeApi;

namespace ByteBait.Api.Tests;

// The Blazor client's login state and account calls when the API is slow, down or answers with an error.
// Like SafeReturnUrlTests these test the client's plain C#. FakeApi plays the API.
public class ClientAccountTests
{
    private const string PlayerJson = """{"id":"6f0f3f5e-52a1-4b0e-9d5c-0c1b7c2a8e41","userName":"tester","roles":[],"money":0}""";
    private const string TokenJson = """{"token":"t"}""";

    [Fact]
    public async Task A_503_from_me_is_not_kept_and_try_again_logs_the_player_back_in()
    {
        var api = new FakeApi(Answer(HttpStatusCode.ServiceUnavailable), Answer(HttpStatusCode.OK, PlayerJson));
        var authentication = new PlayerAuthenticationStateProvider(api.Client);

        var first = await authentication.GetAuthenticationStateAsync();
        Assert.False(first.User.Identity?.IsAuthenticated);
        Assert.True(authentication.CheckFailed);

        authentication.CheckAgain();
        var second = await authentication.GetAuthenticationStateAsync();

        Assert.True(second.User.Identity?.IsAuthenticated);
        Assert.Equal("tester", second.User.Identity?.Name);
        Assert.False(authentication.CheckFailed);
    }

    // A real 401 is an answer: the player is logged out and goes to /login, not to "Try again"
    [Fact]
    public async Task A_401_from_me_means_logged_out()
    {
        var authentication = new PlayerAuthenticationStateProvider(new FakeApi(Answer(HttpStatusCode.Unauthorized)).Client);

        var state = await authentication.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
        Assert.False(authentication.CheckFailed);
    }

    [Fact]
    public async Task A_timeout_on_me_is_a_failed_check_and_not_an_exception()
    {
        var authentication = new PlayerAuthenticationStateProvider(new FakeApi(TimesOut).Client);

        var state = await authentication.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
        Assert.True(authentication.CheckFailed);
    }

    [Fact]
    public async Task A_timeout_on_login_shows_a_message()
    {
        var api = new FakeApi(Answer(HttpStatusCode.OK, TokenJson), TimesOut);
        var accounts = new AccountClient(api.Client, new PlayerAuthenticationStateProvider(api.Client));

        var error = await accounts.LoginAsync(new AccountForm { UserName = "tester", Password = "password123" });

        Assert.Equal("Couldn't reach the server. Try again.", error?.Message);
    }

    // On a shared PC the player must see that the logout wasn't confirmed, and the page must not pretend it was
    [Fact]
    public async Task A_timeout_on_logout_keeps_the_player_signed_in_on_the_page()
    {
        var api = new FakeApi(Answer(HttpStatusCode.OK, TokenJson), TimesOut);
        var authentication = new PlayerAuthenticationStateProvider(api.Client);
        authentication.LoggedIn(new Player(Guid.NewGuid(), "tester", [], 0));
        var accounts = new AccountClient(api.Client, authentication);

        var error = await accounts.LogoutAsync();

        Assert.Equal("Couldn't confirm that you're signed out. Try again.", error?.Message);
        Assert.True((await authentication.GetAuthenticationStateAsync()).User.Identity?.IsAuthenticated);
    }
}
