using ByteBait.Data;
using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ByteBait.Api.Tests;

// A lane with one level that a test can fish on. The lane number and name are unique, so tests don't collide
// with each other or with the seeded lanes 1-4, and everything is deleted again when the test is done.
public sealed class TestLevel : IAsyncDisposable
{
    private readonly ApiFactory _factory;

    private TestLevel(ApiFactory factory, int laneId, int levelId, int laneNumber)
    {
        _factory = factory;
        LaneId = laneId;
        LevelId = levelId;
        LaneNumber = laneNumber;
    }

    public int LaneId { get; }

    public int LevelId { get; }

    public int LaneNumber { get; }

    public const int RequiredFish = 2;

    public static async Task<TestLevel> CreateAsync(ApiFactory factory)
    {
        var lane = new Lane
        {
            Number = Random.Shared.Next(1_000, int.MaxValue),
            Name = $"lake-{Guid.NewGuid():N}"[..30],
            City = "Testby",
            Latitude = 55.0,
            Longitude = 9.7,
        };
        var level = new Level { Lane = lane, Number = 1, RequiredFish = RequiredFish };

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ByteBaitDbContext>();
        db.AddRange(lane, level);
        await db.SaveChangesAsync();

        return new TestLevel(factory, lane.Id, level.Id, lane.Number);
    }

    // Casts on a level can't be deleted through the level (restrict), so they go first. Their catches follow (cascade).
    public async ValueTask DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ByteBaitDbContext>();
        await db.Casts.Where(c => c.LevelId == LevelId).ExecuteDeleteAsync();
        await db.Levels.Where(l => l.Id == LevelId).ExecuteDeleteAsync();
        await db.Lanes.Where(l => l.Id == LaneId).ExecuteDeleteAsync();
    }
}
