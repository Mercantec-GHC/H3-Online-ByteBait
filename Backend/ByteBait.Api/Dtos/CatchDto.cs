using ByteBait.Data.Entities;

namespace ByteBait.Api.Dtos;

// Response body for POST /api/catches (see docs/api-contract.md): the saved result and the level's new progress.
public record CatchDto(int Id, int CastId, string Result, DateTime ResolvedAt, LevelProgressDto Progress)
{
    public static CatchDto From(Catch saved, LevelProgressDto progress) => new(
        saved.Id,
        saved.CastId,
        saved.Result.ToString().ToLowerInvariant(),
        saved.ResolvedAt,
        progress);
}
