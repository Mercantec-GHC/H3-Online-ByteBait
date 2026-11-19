using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByteBait.Data.Configurations;

public class RarityChanceConfiguration : IEntityTypeConfiguration<RarityChance>
{
    public void Configure(EntityTypeBuilder<RarityChance> builder)
    {
        builder.ToTable("rarity_chances", table =>
        {
            // Common has no row, its chance is the rest up to 100 %
            table.HasCheckConstraint("ck_rarity_chances_rarity", "rarity IN ('rare', 'legendary')");
            table.HasCheckConstraint("ck_rarity_chances_base_chance_range", "base_chance >= 0 AND base_chance <= 1");
        });

        builder.HasKey(r => r.Rarity);

        builder.Property(r => r.Rarity).HasLowercaseConversion();

        // The example values from the product description. Can be changed in the admin panel.
        builder.HasData(
            new RarityChance { Rarity = Rarity.Rare, BaseChance = 0.20 },
            new RarityChance { Rarity = Rarity.Legendary, BaseChance = 0.05 });
    }
}
