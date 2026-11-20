using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByteBait.Data.Configurations;

public class LaneConfiguration : IEntityTypeConfiguration<Lane>
{
    public void Configure(EntityTypeBuilder<Lane> builder)
    {
        builder.ToTable("lanes", table =>
        {
            table.HasCheckConstraint("ck_lanes_number_positive", "number >= 1");
            table.HasCheckConstraint("ck_lanes_latitude_range", "latitude >= -90 AND latitude <= 90");
            table.HasCheckConstraint("ck_lanes_longitude_range", "longitude >= -180 AND longitude <= 180");
        });

        builder.HasKey(l => l.Id);

        builder.HasIndex(l => l.Number).IsUnique();

        builder.Property(l => l.Name).HasMaxLength(50);
        builder.Property(l => l.City).HasMaxLength(50);
    }
}
