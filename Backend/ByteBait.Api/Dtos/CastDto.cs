using ByteBait.Data.Entities;

namespace ByteBait.Api.Dtos;

// The fish the cast hooked, picked by the server (S2.8). The game knows it before hooking and the fight.
public record FishDto(int SpeciesId, string Name, string Rarity, double WeightKg, double LengthCm);

// Response body for POST /api/casts (see docs/api-contract.md).
// LevelId and Fish are null for a cast without a level (sprint 1). Fish is also null if the lane has no species yet.
public record CastDto(int Id, string Zone, double Strength, DateTime CastAt, DateTime CreatedAt, int? LevelId, FishDto? Fish)
{
    public static CastDto From(Cast cast) => new(
        cast.Id,
        cast.Zone.ToString().ToLowerInvariant(),
        cast.Strength,
        cast.CastAt,
        cast.CreatedAt,
        cast.LevelId,
        cast.FishSpecies is { } species && cast.WeightKg is { } weight && cast.LengthCm is { } length
            ? new FishDto(species.Id, species.Name, species.Rarity.ToString().ToLowerInvariant(), weight, length)
            : null);
}
