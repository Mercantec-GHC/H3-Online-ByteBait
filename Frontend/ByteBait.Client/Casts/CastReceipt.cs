// The server's cast ID belongs to one level and the exact fish selected for that cast.
namespace ByteBait.Client.Casts;

public sealed record ServerFish(int SpeciesId, string Name, string Rarity, double WeightKg, double LengthCm);

public sealed record HookedCast(int CastId, int LevelId, ServerFish Fish);

internal sealed record CastReceipt(int Id, int? LevelId, ServerFish? Fish)
{
    // A 201 also covers legacy casts and levels with no species. Only a complete fish can enter the fight.
    public HookedCast? ForLevel(int expectedLevelId) =>
        Id > 0 && LevelId == expectedLevelId && Fish is { SpeciesId: > 0, WeightKg: > 0, LengthCm: > 0 }
        && !string.IsNullOrWhiteSpace(Fish.Name) && Fish.Rarity is "common" or "rare" or "legendary"
        && double.IsFinite(Fish.WeightKg) && double.IsFinite(Fish.LengthCm)
            ? new HookedCast(Id, expectedLevelId, Fish)
            : null;
}
