using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ByteBait.Data;

public class ByteBaitDbContext(DbContextOptions<ByteBaitDbContext> options) : DbContext(options)
{
    public DbSet<Cast> Casts => Set<Cast>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration in the Configurations folder
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ByteBaitDbContext).Assembly);
    }
}
