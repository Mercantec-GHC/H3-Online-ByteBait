// Register, login and logout against /api/account (see docs/api-contract.md, "Konto")
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ByteBait.Client.Accounts;

// What went wrong, ready to show in the form. FieldErrors uses the API's field names ("userName", "password").
public sealed record AccountError(string Message, IReadOnlyDictionary<string, string[]> FieldErrors);

public sealed class AccountClient(HttpClient http, PlayerAuthenticationStateProvider authentication)
{
    // Null when it worked
    public Task<AccountError?> RegisterAsync(AccountForm form) => SignInAsync("api/account/register", form);

    public Task<AccountError?> LoginAsync(AccountForm form) => SignInAsync("api/account/login", form);

    public async Task LogoutAsync()
    {
        try
        {
            using var response = await PostAsync("api/account/logout", content: null);
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

    private async Task<AccountError?> SignInAsync(string url, AccountForm form)
    {
        try
        {
            using var response = await PostAsync(url, JsonContent.Create(form, AccountJsonContext.Default.AccountForm));
            if (response.IsSuccessStatusCode)
            {
                var player = await response.Content.ReadFromJsonAsync(AccountJsonContext.Default.Player);
                authentication.LoggedIn(player!);
                return null;
            }

            return await ErrorFromAsync(response);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
        {
            return new AccountError("Couldn't reach the server. Try again.", NoFieldErrors);
        }
    }

    // Every POST needs the antiforgery token. It belongs to the player, so a new one is fetched every time
    // instead of keeping one that stops working after login or logout.
    private async Task<HttpResponseMessage> PostAsync(string url, HttpContent? content)
    {
        var token = await http.GetFromJsonAsync("api/account/antiforgery", AccountJsonContext.Default.AntiforgeryTokenResponse);

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Add("X-XSRF-TOKEN", token!.Token);
        return await http.SendAsync(request);
    }

    private static async Task<AccountError> ErrorFromAsync(HttpResponseMessage response)
    {
        Problem? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync(AccountJsonContext.Default.Problem);
        }
        catch (JsonException)
        {
            // Not a ProblemDetails, the status code below is enough
        }

        var message = response.StatusCode switch
        {
            HttpStatusCode.BadRequest when problem?.Errors is { Count: > 0 } => "Check the fields below.",
            HttpStatusCode.TooManyRequests => problem?.Detail ?? "Too many failed attempts. Try again later.",
            _ => problem?.Title ?? "Something went wrong. Try again.",
        };

        return new AccountError(message, problem?.Errors ?? NoFieldErrors);
    }

    private static readonly IReadOnlyDictionary<string, string[]> NoFieldErrors = new Dictionary<string, string[]>();
}
