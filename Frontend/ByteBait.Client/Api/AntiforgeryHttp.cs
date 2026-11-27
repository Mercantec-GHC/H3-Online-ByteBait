using System.Net.Http.Json;

namespace ByteBait.Client.Api;

public static class AntiforgeryHttp
{
    // Every POST, PUT and DELETE needs the antiforgery token (see docs/api-contract.md, "Antiforgery").
    // It belongs to the player, so a new one is fetched every time instead of keeping one that stops
    // working after login or logout.
    public static async Task<HttpResponseMessage> SendWithTokenAsync(
        this HttpClient http, HttpMethod method, string url, HttpContent? content = null)
    {
        var token = await http.GetFromJsonAsync("api/account/antiforgery", ApiJsonContext.Default.AntiforgeryTokenResponse);

        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add("X-XSRF-TOKEN", token!.Token);
        return await http.SendAsync(request);
    }
}
