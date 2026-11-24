using ByteBait.Data.Seed;
using Microsoft.EntityFrameworkCore;

namespace ByteBait.Data.Tests;

// Checks that the seed data ends up in a real PostgreSQL database
[Collection(PostgresCollection.Name)]
public class SeededDatabaseTests(PostgresDatabase database)
{
    [PostgresFact]
    public async Task Lanes_are_seeded_with_levels_and_species()
    {
        await using var db = database.CreateContext();

        // Other tests add their own lanes, so only look at the seeded ones
        var laneIds = LaneSeed.All.Select(l => l.Id).ToList();
        var lanes = await db.Lanes
            .Include(l => l.Levels)
            .Include(l => l.FishSpecies)
            .Where(l => laneIds.Contains(l.Id))
            .OrderBy(l => l.Number)
            .ToListAsync();

        Assert.Equal(LaneSeed.All.Select(l => l.Name), lanes.Select(l => l.Name));
        Assert.All(lanes, l => Assert.Equal(2, l.Levels.Count));
        Assert.All(lanes, l => Assert.Equal(6, l.FishSpecies.Count));
    }

    [PostgresFact]
    public async Task Equipment_is_seeded()
    {
        await using var db = database.CreateContext();

        var ids = EquipmentSeed.All.Select(e => e.Id).ToList();
        var names = await db.Equipment
            .Where(e => ids.Contains(e.Id))
            .OrderBy(e => e.Id)
            .Select(e => e.Name)
            .ToListAsync();

        Assert.Equal(EquipmentSeed.All.Select(e => e.Name), names);
    }

    // The seeded ids are fixed, so the id counters have to continue after them.
    // Otherwise the first species created in the admin panel would get id 1 and be rejected.
    [PostgresFact]
    public async Task New_rows_get_ids_after_the_seeded_ones()
    {
        await using var data = await TestWorld.CreateAsync(database);

        Assert.True(data.Lane.Id > LaneSeed.All.Max(l => l.Id));
        Assert.True(data.Level.Id > LevelSeed.All.Max(l => l.Id));
        Assert.True(data.Species.Id > FishSpeciesSeed.All.Max(f => f.Id));
    }
}
