using ByteBait.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ByteBait.Data.Configurations;

public class LevelConfiguration : IEntityTypeConfiguration<Level>
{
    public void Configure(EntityTypeBuilder<Level> builder)
    {
        builder.ToTable("levels", table =>
        {
            table.HasCheckConstraint("ck_levels_number_positive", "number >= 1");
            table.HasCheckConstraint("ck_levels_required_fish_positive", "required_fish >= 1");
        });

        builder.HasKey(l => l.Id);

        // Level 1 and 2 only once per lane
        builder.HasIndex(l => new { l.LaneId, l.Number }).IsUnique();

        builder.HasOne(l => l.Lane)
            .WithMany(l => l.Levels)
            .HasForeignKey(l => l.LaneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
