// Register, login and logout against /api/account (see docs/api-contract.md, "Konto")
using System.Net.Http.Json;
using System.Text.Json;
using ByteBait.Client.Api;

namespace ByteBait.Client.Accounts;

public sealed class AccountClient(HttpClient http, PlayerAuthenticationStateProvider authentication)
{
    // Null when it worked
    public Task<ApiError?> RegisterAsync(AccountForm form) => SignInAsync("api/account/register", form);

    public Task<ApiError?> LoginAsync(AccountForm form) => SignInAsync("api/account/login", form);

    public async Task LogoutAsync()
    {
        try
        {
            using var response = await http.SendWithTokenAsync(HttpMethod.Post, "api/account/logout");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
        {
            // Nothing to show: the player is logged out on this page either way
        }
        finally
        {
            // The cookie may still be there if the request failed, but the page shouldn't act logged in
            authentication.LoggedOutNow();
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
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
        {
            return ApiError.NoConnection;
        }
    }
}
