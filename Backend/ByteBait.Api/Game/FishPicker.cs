using ByteBait.Data.Entities;

namespace ByteBait.Api.Game;

// The fish a cast hooks, picked by the server so the game can't cheat (docs/api-contract.md, S2.8).
public record PickedFish(FishSpecies Species, double WeightKg, double LengthCm);

// Picks the fish for a cast on a lane. Pure logic without the database, so the chances can be checked in isolation.
//
// 1. Depth: a soft cast (strength below DeepWaterFrom) lands in shallow water, a hard cast in deep water.
//    Only species at that depth can bite. If the lane has none there, any species on the lane can.
// 2. Rarity: rare = base chance × zone multiplier, legendary = base chance × zone multiplier,
//    common = the rest up to 100 %. Red ×0 therefore only gives common fish.
// 3. If no species at the depth has the rolled rarity, the next lower rarity is used (legendary → rare → common),
//    and if there is none of those either, the next higher. A cast always hooks a fish if the lane has any species.
// 4. Species: one of the matching species, all equally likely.
// 5. Size: weight and length are drawn between the species' min and max with the same random fraction,
//    so a heavy fish is also a long one.
public class FishPicker(Random random)
{
    // BESLUTNING (Bjarke, S2.8): strength 0.0–0.49 is a soft cast (shallow), 0.5–1.0 a hard cast (deep).
    public const double DeepWaterFrom = 0.5;

    public static WaterDepth DepthFor(double strength) => strength < DeepWaterFrom ? WaterDepth.Shallow : WaterDepth.Deep;

    // Chance of each rarity for a cast. The three chances always add up to 1.
    public static IReadOnlyDictionary<Rarity, double> ChancesFor(IReadOnlyDictionary<Rarity, double> baseChances, double zoneMultiplier)
    {
        var rare = baseChances.GetValueOrDefault(Rarity.Rare) * zoneMultiplier;
        var legendary = baseChances.GetValueOrDefault(Rarity.Legendary) * zoneMultiplier;

        // The admin panel (S3.7) must keep the sum at or below 100 %. If it doesn't, scale down so it still adds up.
        var special = rare + legendary;
        if (special > 1)
        {
            rare /= special;
            legendary /= special;
        }

        return new Dictionary<Rarity, double>
        {
            [Rarity.Common] = Math.Max(0, 1 - rare - legendary),
            [Rarity.Rare] = rare,
            [Rarity.Legendary] = legendary,
        };
    }

    // Null only if the lane has no species at all
    public PickedFish? Pick(
        IReadOnlyCollection<FishSpecies> laneSpecies,
        double strength,
        IReadOnlyDictionary<Rarity, double> baseChances,
        double zoneMultiplier)
    {
        if (laneSpecies.Count == 0)
        {
            return null;
        }

        var depth = DepthFor(strength);
        var atDepth = laneSpecies.Where(s => s.Depth == depth).ToList();
        var candidates = atDepth.Count > 0 ? atDepth : laneSpecies.ToList();

        var rolled = RollRarity(ChancesFor(baseChances, zoneMultiplier));
        var rarity = FallbackOrder(rolled).First(r => candidates.Any(s => s.Rarity == r));
        var matching = candidates.Where(s => s.Rarity == rarity).ToList();
        var species = matching[random.Next(matching.Count)];

        var size = random.NextDouble();
        return new PickedFish(
            species,
            WeightKg: Math.Round(Between(species.MinWeightKg, species.MaxWeightKg, size), 2),
            LengthCm: Math.Round(Between(species.MinLengthCm, species.MaxLengthCm, size), 1));
    }

    private Rarity RollRarity(IReadOnlyDictionary<Rarity, double> chances)
    {
        var roll = random.NextDouble();
        if (roll < chances[Rarity.Legendary])
        {
            return Rarity.Legendary;
        }

        return roll < chances[Rarity.Legendary] + chances[Rarity.Rare] ? Rarity.Rare : Rarity.Common;
    }

    // The rolled rarity first, then lower, then higher
    private static IEnumerable<Rarity> FallbackOrder(Rarity rolled) => rolled switch
    {
        Rarity.Legendary => [Rarity.Legendary, Rarity.Rare, Rarity.Common],
        Rarity.Rare => [Rarity.Rare, Rarity.Common, Rarity.Legendary],
        _ => [Rarity.Common, Rarity.Rare, Rarity.Legendary],
    };

    private static double Between(double min, double max, double fraction) => min + (max - min) * fraction;
}
