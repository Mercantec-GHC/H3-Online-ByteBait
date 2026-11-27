// Register, login and logout against /api/account (see docs/api-contract.md, "Konto")
using System.Net.Http.Json;
using ByteBait.Client.Api;

namespace ByteBait.Client.Accounts;

public sealed class AccountClient(HttpClient http, PlayerAuthenticationStateProvider authentication)
{
    private static readonly ApiError LogoutFailed =
        new("Couldn't confirm that you're signed out. Try again.", new Dictionary<string, string[]>());

    // Null when it worked
    public Task<ApiError?> RegisterAsync(AccountForm form) => SignInAsync("api/account/register", form);

    public Task<ApiError?> LoginAsync(AccountForm form) => SignInAsync("api/account/login", form);

    // Only logged out on this page once the API has removed the cookie. Otherwise the next person on a shared PC
    // could reload the page and be logged in as this player.
    public async Task<ApiError?> LogoutAsync()
    {
        try
        {
            using var response = await http.SendWithTokenAsync(HttpMethod.Post, "api/account/logout");
            if (!response.IsSuccessStatusCode)
            {
                return LogoutFailed;
            }

            authentication.LoggedOutNow();
            return null;
        }
        catch (Exception ex) when (ApiError.IsConnectionError(ex))
        {
            return LogoutFailed;
        }
    }

    private async Task<ApiError?> SignInAsync(string url, AccountForm form)
    {
        try
        {
            using var response = await http.SendWithTokenAsync(
                HttpMethod.Post, url, JsonContent.Create(form, ApiJsonContext.Default.AccountForm));

            if (!response.IsSuccessStatusCode)
            {
                return await ApiError.FromAsync(response);
            }

            var player = await response.Content.ReadFromJsonAsync(ApiJsonContext.Default.Player);
            authentication.LoggedIn(player!);
            return null;
        }
        catch (Exception ex) when (ApiError.IsConnectionError(ex))
        {
            return ApiError.NoConnection;
        }
    }
}
