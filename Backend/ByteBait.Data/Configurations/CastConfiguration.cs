using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByteBait.Data.Configurations;

public class CastConfiguration : IEntityTypeConfiguration<Cast>
{
    public void Configure(EntityTypeBuilder<Cast> builder)
    {
        // Check constraints, so bad values can't be saved even if the API forgets to validate them
        builder.ToTable("casts", table =>
        {
            table.HasCheckConstraint("ck_casts_strength_range", "strength >= 0 AND strength <= 1");
            table.HasCheckConstraint("ck_casts_zone", "zone IN ('red', 'yellow', 'green')");
        });

        builder.HasKey(c => c.Id);

        // Store the zone as "red"/"yellow"/"green" instead of 0/1/2 (same values as the API contract)
        builder.Property(c => c.Zone)
            .HasConversion(
                zone => zone.ToString().ToLowerInvariant(),
                text => Enum.Parse<CastZone>(text, ignoreCase: true))
            .HasMaxLength(10);

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("now()");
    }
}
