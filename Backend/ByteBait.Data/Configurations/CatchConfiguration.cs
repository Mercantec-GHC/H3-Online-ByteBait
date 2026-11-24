using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByteBait.Data.Configurations;

public class CatchConfiguration : IEntityTypeConfiguration<Catch>
{
    public void Configure(EntityTypeBuilder<Catch> builder)
    {
        builder.ToTable("catches", table =>
        {
            table.HasCheckConstraint("ck_catches_result", LowercaseEnum.Check<CatchResult>("result"));

            // Only a fish that was caught can be sold or put in the aquarium, and not both
            table.HasCheckConstraint("ck_catches_only_caught_is_kept",
                "result = 'caught' OR (sold_at IS NULL AND NOT in_aquarium)");
            table.HasCheckConstraint("ck_catches_sold_or_aquarium", "NOT (in_aquarium AND sold_at IS NOT NULL)");

            // A sale has both a time and a price
            table.HasCheckConstraint("ck_catches_sale_complete", "(sold_at IS NULL) = (sold_for IS NULL)");
            table.HasCheckConstraint("ck_catches_sold_for_not_negative", "sold_for >= 0");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Result).HasLowercaseConversion();

        builder.Property(c => c.ResolvedAt)
            .HasDefaultValueSql("now()");

        // One catch per cast. The unique index on cast_id stops the game from saving the same fight twice.
        builder.HasOne(c => c.Cast)
            .WithOne(c => c.Catch)
            .HasForeignKey<Catch>(c => c.CastId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
