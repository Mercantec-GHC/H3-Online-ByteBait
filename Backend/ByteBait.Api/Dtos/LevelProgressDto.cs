using ByteBait.Data;
using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ByteBait.Api.Dtos;

// How far the player is on a level (see docs/api-contract.md).
// Progress is not stored: it is counted from the player's caught fish on the level (docs/data-model.md).
public record LevelProgressDto(
    int LevelId,
    int LaneId,
    int LaneNumber,
    int LevelNumber,
    int RequiredFish,
    int CaughtFish,
    bool Completed)
{
    // Null if the level doesn't exist
    public static async Task<LevelProgressDto?> LoadAsync(
        ByteBaitDbContext db, int levelId, Guid? playerId, CancellationToken cancellationToken)
    {
        var level = await db.Levels
            .AsNoTracking()
            .Include(l => l.Lane)
            .SingleOrDefaultAsync(l => l.Id == levelId, cancellationToken);

        if (level is null)
        {
            return null;
        }

        var caughtFish = await db.Catches.CountAsync(
            c => c.Result == CatchResult.Caught && c.Cast.LevelId == levelId && c.Cast.PlayerId == playerId,
            cancellationToken);

        return new LevelProgressDto(
            level.Id,
            level.LaneId,
            level.Lane.Number,
            level.Number,
            level.RequiredFish,
            caughtFish,
            Completed: caughtFish >= level.RequiredFish);
    }
}
