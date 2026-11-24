using ByteBait.Data.Entities;

namespace ByteBait.Api.Dtos;

// Response body for POST /api/casts (see docs/api-contract.md).
// LevelId is null for a cast without a level (sprint 1). The fish comes with S2.8.
public record CastDto(int Id, string Zone, double Strength, DateTime CastAt, DateTime CreatedAt, int? LevelId)
{
    public static CastDto From(Cast cast) => new(
        cast.Id,
        cast.Zone.ToString().ToLowerInvariant(),
        cast.Strength,
        cast.CastAt,
        cast.CreatedAt,
        cast.LevelId);
}
