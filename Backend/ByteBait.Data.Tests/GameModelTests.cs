using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ByteBait.Data.Tests;

// Tests the relations and check constraints from S1.11 against a real PostgreSQL database
[Collection(PostgresCollection.Name)]
public class GameModelTests(PostgresDatabase database)
{
    [PostgresFact]
    public async Task Catch_chances_and_zone_multipliers_are_seeded()
    {
        await using var db = database.CreateContext();

        var chances = await db.RarityChances.ToDictionaryAsync(r => r.Rarity, r => r.BaseChance);
        Assert.Equal(0.20, chances[Rarity.Rare]);
        Assert.Equal(0.05, chances[Rarity.Legendary]);
        Assert.False(chances.ContainsKey(Rarity.Common));

        var multipliers = await db.ZoneMultipliers.ToDictionaryAsync(z => z.Zone, z => z.Multiplier);
        Assert.Equal(0, multipliers[CastZone.Red]);
        Assert.Equal(1, multipliers[CastZone.Yellow]);
        Assert.Equal(1.5, multipliers[CastZone.Green]);
    }

    [PostgresFact]
    public async Task Caught_fish_is_saved_with_player_level_and_species()
    {
        await using var data = await TestWorld.CreateAsync(database);
        await using var db = database.CreateContext();

        var cast = data.NewCast();
        cast.Catch = new Catch { Result = CatchResult.Caught };
        db.Casts.Add(cast);
        await db.SaveChangesAsync();

        await using var readDb = database.CreateContext();
        var saved = await readDb.Catches
            .Include(c => c.Cast).ThenInclude(c => c.FishSpecies)
            .Include(c => c.Cast).ThenInclude(c => c.Level)
            .SingleAsync(c => c.Id == cast.Catch.Id);

        Assert.Equal(data.Player.Id, saved.Cast.PlayerId);
        Assert.Equal(data.Species.Name, saved.Cast.FishSpecies!.Name);
        Assert.Equal(data.Level.Id, saved.Cast.Level!.Id);
        Assert.NotEqual(default, saved.ResolvedAt);
    }

    [PostgresFact]
    public async Task Database_rejects_species_with_max_weight_below_min()
    {
        await using var data = await TestWorld.CreateAsync(database);
        await using var db = database.CreateContext();

        db.FishSpecies.Add(TestWorld.NewSpecies(data.Lane.Id, minWeightKg: 3, maxWeightKg: 1));

        await AssertRejected(db, PostgresErrorCodes.CheckViolation);
    }

    [PostgresFact]
    public async Task Database_rejects_a_fish_that_is_both_sold_and_in_the_aquarium()
    {
        await using var data = await TestWorld.CreateAsync(database);
        await using var db = database.CreateContext();

        var cast = data.NewCast();
        cast.Catch = new Catch
        {
            Result = CatchResult.Caught,
            InAquarium = true,
            SoldAt = DateTime.UtcNow,
            SoldFor = 10,
        };
        db.Casts.Add(cast);

        await AssertRejected(db, PostgresErrorCodes.CheckViolation);
    }

    [PostgresFact]
    public async Task Database_rejects_a_lost_fish_in_the_aquarium()
    {
        await using var data = await TestWorld.CreateAsync(database);
        await using var db = database.CreateContext();

        var cast = data.NewCast();
        cast.Catch = new Catch { Result = CatchResult.Lost, InAquarium = true };
        db.Casts.Add(cast);

        await AssertRejected(db, PostgresErrorCodes.CheckViolation);
    }

    [PostgresFact]
    public async Task Species_that_has_been_fished_cannot_be_deleted()
    {
        await using var data = await TestWorld.CreateAsync(database);
        await using var db = database.CreateContext();

        db.Casts.Add(data.NewCast());
        await db.SaveChangesAsync();

        await using var deleteDb = database.CreateContext();
        deleteDb.FishSpecies.Remove(await deleteDb.FishSpecies.SingleAsync(f => f.Id == data.Species.Id));

        await AssertRejected(deleteDb, PostgresErrorCodes.ForeignKeyViolation);
    }

    [PostgresFact]
    public async Task Database_rejects_a_third_level_on_a_lane()
    {
        await using var data = await TestWorld.CreateAsync(database);
        await using var db = database.CreateContext();

        db.Levels.Add(new Level { LaneId = data.Lane.Id, Number = 3, RequiredFish = 5 });

        await AssertRejected(db, PostgresErrorCodes.CheckViolation);
    }

    [PostgresFact]
    public async Task Database_rejects_a_cast_with_species_but_no_weight()
    {
        await using var data = await TestWorld.CreateAsync(database);
        await using var db = database.CreateContext();

        var cast = data.NewCast();
        cast.WeightKg = null;
        db.Casts.Add(cast);

        await AssertRejected(db, PostgresErrorCodes.CheckViolation);
    }

    [PostgresFact]
    public async Task Database_rejects_negative_money()
    {
        await using var db = database.CreateContext();
        db.Players.Add(new Player { UserName = TestWorld.UniqueName("player"), Money = -1 });

        await AssertRejected(db, PostgresErrorCodes.CheckViolation);
    }

    private static async Task AssertRejected(ByteBaitDbContext db, string sqlState)
    {
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(sqlState, pg.SqlState);
    }
}
