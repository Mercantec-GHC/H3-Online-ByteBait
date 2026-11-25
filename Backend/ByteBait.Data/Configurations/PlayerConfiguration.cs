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

        builder.Property(p => p.UserName).HasMaxLength(32).IsRequired();

        // The username in uppercase, so "Fisker" and "fisker" count as the same name
        builder.Property(p => p.NormalizedUserName).HasMaxLength(32).IsRequired();
        builder.HasIndex(p => p.NormalizedUserName).IsUnique().HasDatabaseName("ix_players_normalized_user_name");

        // An account is only a name and a password, so the rest of Identity's columns are left out
        builder.Ignore(p => p.Email);
        builder.Ignore(p => p.NormalizedEmail);
        builder.Ignore(p => p.EmailConfirmed);
        builder.Ignore(p => p.PhoneNumber);
        builder.Ignore(p => p.PhoneNumberConfirmed);
        builder.Ignore(p => p.TwoFactorEnabled);

        builder.HasIndex(p => p.AquariumShareCode).IsUnique();
        builder.Property(p => p.AquariumShareCode).HasMaxLength(16);

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("now()");
    }
}
