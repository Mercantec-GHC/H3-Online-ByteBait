using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ByteBait.Data;

public class ByteBaitDbContext(DbContextOptions<ByteBaitDbContext> options) : DbContext(options)
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration in the Configurations folder
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ByteBaitDbContext).Assembly);
    }
}
