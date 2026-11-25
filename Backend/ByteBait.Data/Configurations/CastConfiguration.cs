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
            table.HasCheckConstraint("ck_casts_zone", LowercaseEnum.Check<CastZone>("zone"));
            table.HasCheckConstraint("ck_casts_weight_positive", "weight_kg > 0");
            table.HasCheckConstraint("ck_casts_length_positive", "length_cm > 0");

            // The server picks species, weight and length together, so a cast has all three or none
            table.HasCheckConstraint("ck_casts_fish_complete",
                "(fish_species_id IS NULL) = (weight_kg IS NULL) AND (fish_species_id IS NULL) = (length_cm IS NULL)");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Zone).HasLowercaseConversion();

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("now()");

        // Deleting a player also deletes their casts and catches
        builder.HasOne(c => c.Player)
            .WithMany(p => p.Casts)
            .HasForeignKey(c => c.PlayerId)
            .HasConstraintName("fk_casts_players_player_id")
            .OnDelete(DeleteBehavior.Cascade);

        // A level or species that has been fished on can't be deleted, the statistics depend on it
        builder.HasOne(c => c.Level)
            .WithMany()
            .HasForeignKey(c => c.LevelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.FishSpecies)
            .WithMany()
            .HasForeignKey(c => c.FishSpeciesId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
