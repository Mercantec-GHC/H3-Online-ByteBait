// The browser sends only these three fields to the Sprint 1 casts endpoint.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ByteBait.Client.Casts;

internal sealed record SaveCastRequest(string Zone, double Strength, DateTime CastAt);

// Keep the wire format available in the trimmed WebAssembly client.
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(SaveCastRequest))]
internal partial class CastJsonContext : JsonSerializerContext { }
