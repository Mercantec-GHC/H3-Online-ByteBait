using ByteBait.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ByteBait.Data.Tests;

// Runs Identity against the players table, to check that it works with the columns we kept
[Collection(PostgresCollection.Name)]
public class PlayerAccountTests(PostgresDatabase database)
{
    private const string Password = "Passw0rd!";

    [PostgresFact]
    public async Task Player_is_saved_with_an_id_and_a_hashed_password()
    {
        await using var services = CreateServices();
        var players = services.GetRequiredService<UserManager<Player>>();
        var player = new Player { UserName = TestWorld.UniqueName("player") };

        var result = await players.CreateAsync(player, Password);
        Assert.True(result.Succeeded, result.ToString());

        try
        {
            await using var db = database.CreateContext();
            var saved = await db.Players.SingleAsync(p => p.Id == player.Id);

            Assert.NotEqual(Guid.Empty, saved.Id);
            Assert.Equal(0, saved.Money);
            Assert.DoesNotContain(Password, saved.PasswordHash);
            Assert.True(await players.CheckPasswordAsync(player, Password));
            Assert.False(await players.CheckPasswordAsync(player, "wrong"));
        }
        finally
        {
            await players.DeleteAsync(player);
        }
    }

    [PostgresFact]
    public async Task Username_is_taken_even_with_other_letter_case()
    {
        await using var data = await TestWorld.CreateAsync(database);
        await using var services = CreateServices();
        var players = services.GetRequiredService<UserManager<Player>>();

        var result = await players.CreateAsync(new Player { UserName = data.Player.UserName!.ToUpperInvariant() }, Password);

        Assert.Equal(nameof(IdentityErrorDescriber.DuplicateUserName), Assert.Single(result.Errors).Code);
    }

    // Two players who register the same name at the same moment both get past Identity's own check
    [PostgresFact]
    public async Task Database_rejects_the_same_username_twice()
    {
        await using var data = await TestWorld.CreateAsync(database);
        await using var db = database.CreateContext();

        db.Players.Add(new Player
        {
            UserName = data.Player.UserName,
            NormalizedUserName = data.Player.NormalizedUserName,
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState);
    }

    [PostgresFact]
    public async Task Player_can_be_given_the_admin_role()
    {
        await using var data = await TestWorld.CreateAsync(database);
        await using var services = CreateServices();
        var players = services.GetRequiredService<UserManager<Player>>();
        var player = (await players.FindByNameAsync(data.Player.UserName!))!;

        Assert.False(await players.IsInRoleAsync(player, RoleNames.Admin));

        var result = await players.AddToRoleAsync(player, RoleNames.Admin);

        Assert.True(result.Succeeded, result.ToString());
        Assert.True(await players.IsInRoleAsync(player, RoleNames.Admin));
    }

    private ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => database.CreateContext());
        services.AddIdentityCore<Player>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ByteBaitDbContext>();

        return services.BuildServiceProvider();
    }
}
