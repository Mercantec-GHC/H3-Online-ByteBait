using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ByteBait.Data.Tests;

// A lane, a level, a species and a player that a test can fish with.
// Names and numbers are unique, so tests running at the same time don't collide,
// and everything is deleted again when the test is done.
public sealed class TestWorld : IAsyncDisposable
{
    private readonly PostgresDatabase _database;

    private TestWorld(PostgresDatabase database, Lane lane, Level level, FishSpecies species, Player player)
    {
        _database = database;
        Lane = lane;
        Level = level;
        Species = species;
        Player = player;
    }

    public Lane Lane { get; }

    public Level Level { get; }

    public FishSpecies Species { get; }

    public Player Player { get; }

    public static async Task<TestWorld> CreateAsync(PostgresDatabase database)
    {
        var lane = new Lane
        {
            Number = Random.Shared.Next(1_000, int.MaxValue),
            Name = UniqueName("lake"),
            City = "Testby",
            Latitude = 55.0,
            Longitude = 9.7,
        };
        var level = new Level { Lane = lane, Number = 1, RequiredFish = 5 };
        var species = NewSpecies(0, minWeightKg: 1, maxWeightKg: 3);
        species.Lane = lane;
        var player = NewPlayer();

        await using var db = database.CreateContext();
        db.AddRange(lane, level, species, player);
        await db.SaveChangesAsync();

        return new TestWorld(database, lane, level, species, player);
    }

    public static FishSpecies NewSpecies(int laneId, double minWeightKg, double maxWeightKg) => new()
    {
        LaneId = laneId,
        Name = UniqueName("fish"),
        Rarity = Rarity.Common,
        Depth = WaterDepth.Shallow,
        MinWeightKg = minWeightKg,
        MaxWeightKg = maxWeightKg,
        MinLengthCm = 20,
        MaxLengthCm = 60,
        Price = 10,
    };

    // Added without Identity, so what it normally fills in is set here
    public static Player NewPlayer()
    {
        var name = UniqueName("player");
        return new Player
        {
            UserName = name,
            NormalizedUserName = name.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
        };
    }

    public static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..30];

    public Cast NewCast() => new()
    {
        Zone = CastZone.Green,
        Strength = 0.5,
        CastAt = DateTime.UtcNow,
        PlayerId = Player.Id,
        LevelId = Level.Id,
        FishSpeciesId = Species.Id,
        WeightKg = 2.1,
        LengthCm = 41,
    };

    // Deleting the player removes its casts and catches, after that the game data can go
    public async ValueTask DisposeAsync()
    {
        await using var db = _database.CreateContext();
        await db.Players.Where(p => p.Id == Player.Id).ExecuteDeleteAsync();
        await db.FishSpecies.Where(f => f.LaneId == Lane.Id).ExecuteDeleteAsync();
        await db.Levels.Where(l => l.LaneId == Lane.Id).ExecuteDeleteAsync();
        await db.Lanes.Where(l => l.Id == Lane.Id).ExecuteDeleteAsync();
    }
}
