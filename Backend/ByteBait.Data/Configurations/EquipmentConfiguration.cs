using ByteBait.Data.Entities;
using ByteBait.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByteBait.Data.Configurations;

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("equipment", table =>
        {
            table.HasCheckConstraint("ck_equipment_type", LowercaseEnum.Check<EquipmentType>("type"));
            table.HasCheckConstraint("ck_equipment_price_not_negative", "price >= 0");
            table.HasCheckConstraint("ck_equipment_power_not_negative", "power >= 0");
        });

        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.Name).IsUnique();
        builder.Property(e => e.Name).HasMaxLength(50);

        builder.Property(e => e.Type).HasLowercaseConversion();

        builder.HasData(EquipmentSeed.All);
    }
}
