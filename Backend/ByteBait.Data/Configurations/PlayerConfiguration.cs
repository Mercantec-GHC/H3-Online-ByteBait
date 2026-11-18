using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByteBait.Data.Configurations;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("players", table =>
        {
            // Buying equipment can never make the balance negative
            table.HasCheckConstraint("ck_players_money_not_negative", "money >= 0");
        });

        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.UserName).IsUnique();
        builder.Property(p => p.UserName).HasMaxLength(32);

        builder.HasIndex(p => p.AquariumShareCode).IsUnique();
        builder.Property(p => p.AquariumShareCode).HasMaxLength(16);

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("now()");
    }
}
