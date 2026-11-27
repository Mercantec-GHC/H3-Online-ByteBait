// The admin panel's calls to /api/admin (see docs/api-contract.md, "Admin")
using System.Net.Http.Json;
using ByteBait.Client.Api;

namespace ByteBait.Client.Admin;

public sealed class AdminClient(HttpClient http)
{
    public async Task<List<Species>> GetSpeciesAsync() =>
        await http.GetFromJsonAsync("api/admin/species", ApiJsonContext.Default.ListSpecies) ?? [];

    public async Task<List<AdminLane>> GetLanesAsync() =>
        await http.GetFromJsonAsync("api/admin/lanes", ApiJsonContext.Default.ListAdminLane) ?? [];

    // The three below return null when it worked
    public Task<ApiError?> CreateAsync(SpeciesForm form) =>
        SendAsync(HttpMethod.Post, "api/admin/species", JsonContent.Create(form, ApiJsonContext.Default.SpeciesForm));

    public Task<ApiError?> UpdateAsync(int id, SpeciesForm form) =>
        SendAsync(HttpMethod.Put, $"api/admin/species/{id}", JsonContent.Create(form, ApiJsonContext.Default.SpeciesForm));

    public Task<ApiError?> DeleteAsync(int id) => SendAsync(HttpMethod.Delete, $"api/admin/species/{id}");

    private async Task<ApiError?> SendAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        try
        {
            using var response = await http.SendWithTokenAsync(method, url, content);
            return response.IsSuccessStatusCode ? null : await ApiError.FromAsync(response);
        }
        catch (Exception ex) when (ApiError.IsConnectionError(ex))
        {
            return ApiError.NoConnection;
        }
    }
}
