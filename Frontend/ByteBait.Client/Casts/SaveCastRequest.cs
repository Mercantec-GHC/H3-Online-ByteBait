// Practice casts keep the original three fields. A level cast includes the level from the game link.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ByteBait.Client.Casts;

internal sealed record SaveCastRequest(string Zone, double Strength, DateTime CastAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? LevelId);

internal sealed record AntiforgeryToken(string Token);

// Keep the wire format available in the trimmed WebAssembly client.
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(SaveCastRequest))]
[JsonSerializable(typeof(AntiforgeryToken))]
[JsonSerializable(typeof(CastReceipt))]
[JsonSerializable(typeof(HookedCast))]
internal partial class CastJsonContext : JsonSerializerContext { }
