using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByteBait.Data.Configurations;

public class FishSpeciesConfiguration : IEntityTypeConfiguration<FishSpecies>
{
    public void Configure(EntityTypeBuilder<FishSpecies> builder)
    {
        builder.ToTable("fish_species", table =>
        {
            table.HasCheckConstraint("ck_fish_species_rarity", LowercaseEnum.Check<Rarity>("rarity"));
            table.HasCheckConstraint("ck_fish_species_depth", LowercaseEnum.Check<WaterDepth>("depth"));
            table.HasCheckConstraint("ck_fish_species_weight_range", "min_weight_kg > 0 AND max_weight_kg >= min_weight_kg");
            table.HasCheckConstraint("ck_fish_species_length_range", "min_length_cm > 0 AND max_length_cm >= min_length_cm");
            table.HasCheckConstraint("ck_fish_species_price_not_negative", "price >= 0");
        });

        builder.HasKey(f => f.Id);

        builder.HasIndex(f => f.Name).IsUnique();
        builder.Property(f => f.Name).HasMaxLength(50);

        builder.Property(f => f.Rarity).HasLowercaseConversion();
        builder.Property(f => f.Depth).HasLowercaseConversion();

        builder.HasOne(f => f.Lane)
            .WithMany(l => l.FishSpecies)
            .HasForeignKey(f => f.LaneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
