using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByteBait.Data.Configurations;

public class ZoneMultiplierConfiguration : IEntityTypeConfiguration<ZoneMultiplier>
{
    public void Configure(EntityTypeBuilder<ZoneMultiplier> builder)
    {
        builder.ToTable("zone_multipliers", table =>
        {
            table.HasCheckConstraint("ck_zone_multipliers_zone", LowercaseEnum.Check<CastZone>("zone"));
            table.HasCheckConstraint("ck_zone_multipliers_multiplier_not_negative", "multiplier >= 0");
        });

        builder.HasKey(z => z.Zone);

        builder.Property(z => z.Zone).HasLowercaseConversion();

        // The example values from the product description. Can be changed in the admin panel.
        builder.HasData(
            new ZoneMultiplier { Zone = CastZone.Red, Multiplier = 0 },
            new ZoneMultiplier { Zone = CastZone.Yellow, Multiplier = 1 },
            new ZoneMultiplier { Zone = CastZone.Green, Multiplier = 1.5 });
    }
}
