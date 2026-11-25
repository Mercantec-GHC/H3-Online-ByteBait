using ByteBait.Data.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ByteBait.Data;

public class ByteBaitDbContext(DbContextOptions<ByteBaitDbContext> options)
    : IdentityDbContext<Player, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<Lane> Lanes => Set<Lane>();

    public DbSet<Level> Levels => Set<Level>();

    public DbSet<FishSpecies> FishSpecies => Set<FishSpecies>();

    public DbSet<RarityChance> RarityChances => Set<RarityChance>();

    public DbSet<ZoneMultiplier> ZoneMultipliers => Set<ZoneMultiplier>();

    public DbSet<Equipment> Equipment => Set<Equipment>();

    public DbSet<Player> Players => Set<Player>();

    public DbSet<PlayerEquipment> PlayerEquipment => Set<PlayerEquipment>();

    public DbSet<Cast> Casts => Set<Cast>();

    public DbSet<Catch> Catches => Set<Catch>();

    // The keys that protect the login cookie. In the container they would be lost at every deploy,
    // and everyone would be logged out.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // First, so the configurations below can change what Identity sets up
        base.OnModelCreating(modelBuilder);

        // Only used for logging in with Google and the like, which we don't have
        modelBuilder.Ignore<IdentityUserLogin<Guid>>();
        modelBuilder.Ignore<IdentityUserToken<Guid>>();

        // Picks up every IEntityTypeConfiguration in the Configurations folder
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ByteBaitDbContext).Assembly);
    }
}
