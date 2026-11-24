using ByteBait.Api.Game;
using ByteBait.Data.Entities;

namespace ByteBait.Api.Tests;

// The fish selection rules from docs/api-contract.md (S2.8). Pure logic, so no database is needed.
// A fixed seed keeps the distribution tests repeatable.
public class FishPickerTests
{
    private const int Casts = 100_000;

    // Same as the seeded rarity_chances in the database
    private static readonly Dictionary<Rarity, double> BaseChances = new()
    {
        [Rarity.Rare] = 0.20,
        [Rarity.Legendary] = 0.05,
    };

    private static FishSpecies Species(string name, Rarity rarity, WaterDepth depth) => new()
    {
        Name = name,
        Rarity = rarity,
        Depth = depth,
        MinWeightKg = 1,
        MaxWeightKg = 3,
        MinLengthCm = 20,
        MaxLengthCm = 60,
    };

    // One species of each rarity at each depth
    private static readonly FishSpecies[] FullLane =
    [
        Species("shallow common", Rarity.Common, WaterDepth.Shallow),
        Species("shallow rare", Rarity.Rare, WaterDepth.Shallow),
        Species("shallow legendary", Rarity.Legendary, WaterDepth.Shallow),
        Species("deep common", Rarity.Common, WaterDepth.Deep),
        Species("deep rare", Rarity.Rare, WaterDepth.Deep),
        Species("deep legendary", Rarity.Legendary, WaterDepth.Deep),
    ];

    [Theory]
    [InlineData(0.0, WaterDepth.Shallow)]
    [InlineData(0.49, WaterDepth.Shallow)]
    [InlineData(0.5, WaterDepth.Deep)]
    [InlineData(1.0, WaterDepth.Deep)]
    public void DepthFor_splits_soft_and_hard_casts_at_0_5(double strength, WaterDepth expected)
    {
        Assert.Equal(expected, FishPicker.DepthFor(strength));
    }

    [Theory]
    [InlineData(0.0, 1.0, 0.0, 0.0)] // red
    [InlineData(1.0, 0.75, 0.20, 0.05)] // yellow
    [InlineData(1.5, 0.625, 0.30, 0.075)] // green
    public void ChancesFor_multiplies_base_chances_by_the_zone(double multiplier, double common, double rare, double legendary)
    {
        var chances = FishPicker.ChancesFor(BaseChances, multiplier);

        Assert.Equal(common, chances[Rarity.Common], 10);
        Assert.Equal(rare, chances[Rarity.Rare], 10);
        Assert.Equal(legendary, chances[Rarity.Legendary], 10);
    }

    [Fact]
    public void ChancesFor_scales_down_when_rare_and_legendary_exceed_100_percent()
    {
        var chances = FishPicker.ChancesFor(new Dictionary<Rarity, double> { [Rarity.Rare] = 0.6, [Rarity.Legendary] = 0.2 }, 2);

        Assert.Equal(0.0, chances[Rarity.Common], 10);
        Assert.Equal(0.75, chances[Rarity.Rare], 10);
        Assert.Equal(0.25, chances[Rarity.Legendary], 10);
    }

    [Theory]
    [InlineData(0.0, 1.0, 0.0, 0.0)] // red
    [InlineData(1.0, 0.75, 0.20, 0.05)] // yellow
    [InlineData(1.5, 0.625, 0.30, 0.075)] // green
    public void Pick_hits_the_expected_rarity_distribution(double multiplier, double common, double rare, double legendary)
    {
        var picker = new FishPicker(new Random(42));
        var counts = new Dictionary<Rarity, int> { [Rarity.Common] = 0, [Rarity.Rare] = 0, [Rarity.Legendary] = 0 };

        for (var i = 0; i < Casts; i++)
        {
            counts[picker.Pick(FullLane, 0.7, BaseChances, multiplier)!.Species.Rarity]++;
        }

        // Within 1 percentage point of the expected chance
        Assert.InRange(counts[Rarity.Common] / (double)Casts, common - 0.01, common + 0.01);
        Assert.InRange(counts[Rarity.Rare] / (double)Casts, rare - 0.01, rare + 0.01);
        Assert.InRange(counts[Rarity.Legendary] / (double)Casts, legendary - 0.01, legendary + 0.01);
    }

    [Theory]
    [InlineData(0.2, WaterDepth.Shallow)]
    [InlineData(0.8, WaterDepth.Deep)]
    public void Pick_only_hooks_species_at_the_cast_depth(double strength, WaterDepth depth)
    {
        var picker = new FishPicker(new Random(1));

        for (var i = 0; i < 1_000; i++)
        {
            Assert.Equal(depth, picker.Pick(FullLane, strength, BaseChances, 1.5)!.Species.Depth);
        }
    }

    [Fact]
    public void Pick_uses_any_species_on_the_lane_when_none_live_at_the_cast_depth()
    {
        var deepOnly = new[] { Species("deep common", Rarity.Common, WaterDepth.Deep) };

        var fish = new FishPicker(new Random(1)).Pick(deepOnly, 0.1, BaseChances, 1);

        Assert.Same(deepOnly[0], fish!.Species);
    }

    [Fact]
    public void Pick_falls_back_to_rare_when_there_is_no_legendary_species()
    {
        var lane = new[]
        {
            Species("common", Rarity.Common, WaterDepth.Deep),
            Species("rare", Rarity.Rare, WaterDepth.Deep),
        };
        var picker = new FishPicker(new Random(42));
        var rare = 0;

        for (var i = 0; i < Casts; i++)
        {
            if (picker.Pick(lane, 0.7, BaseChances, 1)!.Species.Rarity == Rarity.Rare)
            {
                rare++;
            }
        }

        // The 5 % legendary rolls become rare, so rare is 20 % + 5 %
        Assert.InRange(rare / (double)Casts, 0.24, 0.26);
    }

    [Fact]
    public void Pick_falls_back_to_a_higher_rarity_when_there_is_nothing_lower()
    {
        var lane = new[] { Species("legendary", Rarity.Legendary, WaterDepth.Deep) };
        var picker = new FishPicker(new Random(1));

        // Red only rolls common, but the lane only has a legendary species
        for (var i = 0; i < 100; i++)
        {
            Assert.Same(lane[0], picker.Pick(lane, 0.7, BaseChances, 0)!.Species);
        }
    }

    [Fact]
    public void Pick_returns_null_for_a_lane_without_species()
    {
        Assert.Null(new FishPicker(new Random(1)).Pick([], 0.7, BaseChances, 1));
    }

    [Fact]
    public void Pick_draws_weight_and_length_within_the_species_limits_and_together()
    {
        var picker = new FishPicker(new Random(7));

        for (var i = 0; i < 1_000; i++)
        {
            var fish = picker.Pick(FullLane, 0.7, BaseChances, 1)!;

            Assert.InRange(fish.WeightKg, 1, 3);
            Assert.InRange(fish.LengthCm, 20, 60);

            // Same random fraction for both, so they sit at the same point in their range (up to rounding)
            var weightFraction = (fish.WeightKg - 1) / 2;
            var lengthFraction = (fish.LengthCm - 20) / 40;
            Assert.Equal(weightFraction, lengthFraction, 0.01);
        }
    }
}
