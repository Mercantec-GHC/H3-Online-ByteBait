// Tells Blazor who is logged in, so pages can use [Authorize] and <AuthorizeView>.
// The login cookie is HttpOnly, so the browser can't read it: GET /api/account/me is the only way to know.
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using ByteBait.Client.Api;
using Microsoft.AspNetCore.Components.Authorization;

namespace ByteBait.Client.Accounts;

public sealed class PlayerAuthenticationStateProvider(HttpClient http) : AuthenticationStateProvider
{
    private static readonly AuthenticationState LoggedOut = new(new ClaimsPrincipal(new ClaimsIdentity()));

    // Asked once, then kept until the player logs in or out
    private Task<AuthenticationState>? state;

    // True when the last check got no real answer (no connection, timeout, or a 5xx while the API or Neon wakes up).
    // The player may still be logged in, so pages show "Try again" instead of sending them to /login.
    public bool CheckFailed { get; private set; }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => state ??= LoadAsync();

    // Asks GET /api/account/me again, e.g. from the "Try again" button after a failed check
    public void CheckAgain() => Set(LoadAsync());

    // Called after login and register, which already answer with the player
    public void LoggedIn(Player player)
    {
        CheckFailed = false;
        Set(Task.FromResult(StateFor(player)));
    }

    public void LoggedOutNow()
    {
        CheckFailed = false;
        Set(Task.FromResult(LoggedOut));
    }

    // The API answered 401, so the login cookie has expired. Only acts if the page still shows a player,
    // so a wrong password or the first GET /api/account/me doesn't replace the state.
    public void LoginExpired()
    {
        if (state is { IsCompletedSuccessfully: true } current && current.Result.User.Identity?.IsAuthenticated == true)
        {
            LoggedOutNow();
        }
    }

    private void Set(Task<AuthenticationState> newState)
    {
        state = newState;
        NotifyAuthenticationStateChanged(newState);
    }

    private async Task<AuthenticationState> LoadAsync()
    {
        CheckFailed = false;
        try
        {
            using var response = await http.GetAsync("api/account/me");

            // Only 200 and 401 are real answers: logged in, or not
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return LoggedOut;
            }

            if (response.StatusCode == HttpStatusCode.OK
                && await response.Content.ReadFromJsonAsync(ApiJsonContext.Default.Player) is { } player)
            {
                return StateFor(player);
            }
        }
        catch (Exception ex) when (ApiError.IsConnectionError(ex))
        {
            // No connection, a timeout, or no API at all (the standalone client)
        }

        CheckFailed = true;
        return LoggedOut;
    }

    private static AuthenticationState StateFor(Player player)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, player.Id.ToString()),
            new(ClaimTypes.Name, player.UserName),
        };
        claims.AddRange(player.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "cookie")));
    }
}
