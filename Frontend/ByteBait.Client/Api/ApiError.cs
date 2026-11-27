// An error from the API, ready to show in a form (see docs/api-contract.md, "Fejlsvar")
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ByteBait.Client.Api;

// FieldErrors uses the API's JSON field names, e.g. "userName" or "maxWeightKg"
public sealed record ApiError(string Message, IReadOnlyDictionary<string, string[]> FieldErrors)
{
    private static readonly IReadOnlyDictionary<string, string[]> NoFieldErrors = new Dictionary<string, string[]>();

    public static ApiError NoConnection { get; } = new("Couldn't reach the server. Try again.", NoFieldErrors);

    public IEnumerable<string> For(string field) =>
        FieldErrors.TryGetValue(field, out var messages) ? messages : [];

    // Reads the ProblemDetails body. Every error from the API has one.
    public static async Task<ApiError> FromAsync(HttpResponseMessage response)
    {
        Problem? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync(ApiJsonContext.Default.Problem);
        }
        catch (JsonException)
        {
            // Not a ProblemDetails, the status code below is enough
        }

        var message = response.StatusCode switch
        {
            HttpStatusCode.BadRequest when problem?.Errors is { Count: > 0 } => "Check the fields below.",
            HttpStatusCode.TooManyRequests => problem?.Detail ?? "Too many failed attempts. Try again later.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden when problem?.Title is null => "You need to sign in again.",
            _ => problem?.Detail ?? problem?.Title ?? "Something went wrong. Try again.",
        };

        return new ApiError(message, problem?.Errors ?? NoFieldErrors);
    }
}
