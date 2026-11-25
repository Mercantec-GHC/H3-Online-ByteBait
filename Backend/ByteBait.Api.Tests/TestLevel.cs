using ByteBait.Data;
using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ByteBait.Api.Tests;

// A lane with one level that a test can fish on, and by default one species on deep water.
// The lane number and the names are unique, so tests don't collide with each other or with the seeded lanes 1-4,
// and everything is deleted again when the test is done.
public sealed class TestLevel : IAsyncDisposable
{
    public const int RequiredFish = 2;

    // The species' size limits, so a test can check that the fish is drawn within them
    public const double MinWeightKg = 1;
    public const double MaxWeightKg = 3;
    public const double MinLengthCm = 20;
    public const double MaxLengthCm = 60;

    private readonly ApiFactory _factory;

    private TestLevel(ApiFactory factory, Lane lane, Level level, FishSpecies? species)
    {
        _factory = factory;
        LaneId = lane.Id;
        LaneNumber = lane.Number;
        LevelId = level.Id;
        Species = species;
    }

    public int LaneId { get; }

    public int LevelId { get; }

    public int LaneNumber { get; }

    // Null for a lane created without species
    public FishSpecies? Species { get; }

    public static async Task<TestLevel> CreateAsync(ApiFactory factory, bool withSpecies = true)
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

        // Deep water, but a soft cast still hooks it, because it is the only species on the lane
        var species = withSpecies
            ? new FishSpecies
            {
                Lane = lane,
                Name = $"fish-{Guid.NewGuid():N}"[..30],
                Rarity = Rarity.Common,
                Depth = WaterDepth.Deep,
                MinWeightKg = MinWeightKg,
                MaxWeightKg = MaxWeightKg,
                MinLengthCm = MinLengthCm,
                MaxLengthCm = MaxLengthCm,
                Price = 10,
            }
            : null;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ByteBaitDbContext>();
        db.AddRange(lane, level);
        if (species is not null)
        {
            db.Add(species);
        }

        await db.SaveChangesAsync();

        return new TestLevel(factory, lane, level, species);
    }

    // Casts can't be deleted through the level or species (restrict), so they go first. Their catches follow (cascade).
    public async ValueTask DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ByteBaitDbContext>();
        await db.Casts.Where(c => c.LevelId == LevelId).ExecuteDeleteAsync();
        await db.FishSpecies.Where(f => f.LaneId == LaneId).ExecuteDeleteAsync();
        await db.Levels.Where(l => l.Id == LevelId).ExecuteDeleteAsync();
        await db.Lanes.Where(l => l.Id == LaneId).ExecuteDeleteAsync();
    }
}
