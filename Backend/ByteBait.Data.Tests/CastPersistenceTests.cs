using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ByteBait.Data.Tests;

[Collection(PostgresCollection.Name)]
public class CastPersistenceTests(PostgresDatabase database)
{
    [PostgresFact]
    public async Task Cast_is_saved_and_read_back()
    {
        var castAt = new DateTime(2026, 11, 17, 9, 15, 30, DateTimeKind.Utc);
        int id;

        await using (var db = database.CreateContext())
        {
            var cast = new Cast { Zone = CastZone.Green, Strength = 0.8, CastAt = castAt };
            db.Casts.Add(cast);
            await db.SaveChangesAsync();
            id = cast.Id;
        }

        await using (var db = database.CreateContext())
        {
            var saved = await db.Casts.SingleAsync(c => c.Id == id);
            Assert.Equal(CastZone.Green, saved.Zone);
            Assert.Equal(0.8, saved.Strength);
            Assert.Equal(castAt, saved.CastAt);
            Assert.NotEqual(default, saved.CreatedAt);

            // Check the raw column value too, it should be the text "green"
            var zoneText = await db.Database
                .SqlQuery<string>($"SELECT zone AS \"Value\" FROM casts WHERE id = {id}")
                .SingleAsync();
            Assert.Equal("green", zoneText);

            db.Casts.Remove(saved);
            await db.SaveChangesAsync();
        }
    }

    [PostgresFact]
    public async Task Database_rejects_strength_outside_0_to_1()
    {
        await using var db = database.CreateContext();
        db.Casts.Add(new Cast { Zone = CastZone.Red, Strength = 1.5, CastAt = DateTime.UtcNow });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, pg.SqlState);
    }
}
