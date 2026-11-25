using ByteBait.Data.Entities;
using ByteBait.Data.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByteBait.Data.Configurations;

// Gives Identity's own tables and keys names like the rest of the database,
// instead of AspNetRoles, fk_player_roles_asp_net_users_user_id and so on. Player has its own configuration.
public class IdentityConfiguration :
    IEntityTypeConfiguration<IdentityRole<Guid>>,
    IEntityTypeConfiguration<IdentityUserRole<Guid>>,
    IEntityTypeConfiguration<IdentityUserClaim<Guid>>,
    IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder)
    {
        builder.ToTable("roles");

        builder.HasIndex(r => r.NormalizedName).IsUnique().HasDatabaseName("ix_roles_normalized_name");

        builder.HasData(RoleSeed.All);
    }

    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder)
    {
        builder.ToTable("player_roles");

        builder.Property(ur => ur.UserId).HasColumnName("player_id");
        builder.HasOne<Player>()
            .WithMany()
            .HasForeignKey(ur => ur.UserId)
            .HasConstraintName("fk_player_roles_players_player_id");
    }

    // The game has no claims of its own, but Identity reads both claim tables at login
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.ToTable("player_claims");

        builder.Property(uc => uc.UserId).HasColumnName("player_id");
        builder.HasIndex(uc => uc.UserId).HasDatabaseName("ix_player_claims_player_id");
        builder.HasOne<Player>()
            .WithMany()
            .HasForeignKey(uc => uc.UserId)
            .HasConstraintName("fk_player_claims_players_player_id");
    }

    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder)
    {
        builder.ToTable("role_claims");
    }
}
