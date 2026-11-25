using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByteBait.Data.Configurations;

public class PlayerEquipmentConfiguration : IEntityTypeConfiguration<PlayerEquipment>
{
    public void Configure(EntityTypeBuilder<PlayerEquipment> builder)
    {
        builder.ToTable("player_equipment");

        // A player owns each item at most once
        builder.HasKey(pe => new { pe.PlayerId, pe.EquipmentId });

        builder.Property(pe => pe.PurchasedAt)
            .HasDefaultValueSql("now()");

        builder.HasOne(pe => pe.Player)
            .WithMany(p => p.Equipment)
            .HasForeignKey(pe => pe.PlayerId)
            .HasConstraintName("fk_player_equipment_players_player_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Equipment that someone owns can't be deleted from the shop
        builder.HasOne(pe => pe.Equipment)
            .WithMany()
            .HasForeignKey(pe => pe.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
