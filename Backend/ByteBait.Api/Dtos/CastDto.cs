using ByteBait.Data.Entities;

namespace ByteBait.Api.Dtos;

// Response body for POST /api/casts (see docs/api-contract.md).
public record CastDto(int Id, string Zone, double Strength, DateTime CastAt, DateTime CreatedAt)
{
    public static CastDto From(Cast cast) => new(
        cast.Id,
        cast.Zone.ToString().ToLowerInvariant(),
        cast.Strength,
        cast.CastAt,
        cast.CreatedAt);
}
