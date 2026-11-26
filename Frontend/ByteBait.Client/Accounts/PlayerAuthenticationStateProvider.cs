// Tells Blazor who is logged in, so pages can use [Authorize] and <AuthorizeView>.
// The login cookie is HttpOnly, so the browser can't read it: GET /api/account/me is the only way to know.
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace ByteBait.Client.Accounts;

public sealed class PlayerAuthenticationStateProvider(HttpClient http) : AuthenticationStateProvider
{
    private static readonly AuthenticationState LoggedOut = new(new ClaimsPrincipal(new ClaimsIdentity()));

    // Asked once, then kept until the player logs in or out
    private Task<AuthenticationState>? state;

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => state ??= LoadAsync();

    // Called after login and register, which already answer with the player
    public void LoggedIn(Player player) => Set(Task.FromResult(StateFor(player)));

    public void LoggedOutNow() => Set(Task.FromResult(LoggedOut));

    private void Set(Task<AuthenticationState> newState)
    {
        state = newState;
        NotifyAuthenticationStateChanged(newState);
    }

    private async Task<AuthenticationState> LoadAsync()
    {
        try
        {
            using var response = await http.GetAsync("api/account/me");
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return LoggedOut;
            }

            var player = await response.Content.ReadFromJsonAsync(AccountJsonContext.Default.Player);
            return player is null ? LoggedOut : StateFor(player);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
        {
            // No API (the standalone client) or no connection: treat it as logged out
            return LoggedOut;
        }
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
