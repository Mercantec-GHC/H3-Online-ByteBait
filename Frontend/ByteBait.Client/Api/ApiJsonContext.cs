// The JSON the client reads and writes for accounts and the admin panel.
// Generated at build time, so it still works in the trimmed WebAssembly client.
using System.Text.Json;
using System.Text.Json.Serialization;
using ByteBait.Client.Accounts;
using ByteBait.Client.Admin;

namespace ByteBait.Client.Api;

// The error responses from the API (ProblemDetails)
internal sealed record Problem(string? Title, string? Detail, Dictionary<string, string[]>? Errors);

internal sealed record AntiforgeryTokenResponse(string Token);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(Problem))]
[JsonSerializable(typeof(AntiforgeryTokenResponse))]
[JsonSerializable(typeof(Player))]
[JsonSerializable(typeof(AccountForm))]
[JsonSerializable(typeof(List<Species>))]
[JsonSerializable(typeof(Species))]
[JsonSerializable(typeof(List<AdminLane>))]
[JsonSerializable(typeof(SpeciesForm))]
internal partial class ApiJsonContext : JsonSerializerContext { }
