using ByteBait.Data.Entities;
using static ByteBait.Data.Entities.Rarity;
using static ByteBait.Data.Entities.WaterDepth;

namespace ByteBait.Data.Seed;

// One common, one rare and one legendary species at each depth on every lane.
// A cast only reaches one depth, so without all six some casts would have no fish to pick.
// Fish with the same rarity and depth sell for more on the next lane.
internal static class FishSpeciesSeed
{
    public static readonly IReadOnlyList<FishSpecies> All =
    [
        // id, lane, name, rarity, depth, min and max weight (kg), min and max length (cm), price
        Species(1, LaneSeed.Nordborg, "Perch", Common, Shallow, 0.1, 1.2, 15, 40, 10),
        Species(2, LaneSeed.Nordborg, "Tench", Rare, Shallow, 0.8, 3.5, 30, 60, 30),
        Species(3, LaneSeed.Nordborg, "Pike", Legendary, Shallow, 3, 14, 60, 120, 100),
        Species(4, LaneSeed.Nordborg, "Bream", Common, Deep, 0.5, 3.5, 25, 60, 12),
        Species(5, LaneSeed.Nordborg, "Zander", Rare, Deep, 1, 7, 40, 85, 35),
        Species(6, LaneSeed.Nordborg, "Wels Catfish", Legendary, Deep, 10, 45, 100, 200, 120),

        Species(7, LaneSeed.Silkeborg, "Grayling", Common, Shallow, 0.3, 1.5, 25, 48, 18),
        Species(8, LaneSeed.Silkeborg, "Trout", Rare, Shallow, 0.5, 4, 30, 65, 50),
        Species(9, LaneSeed.Silkeborg, "Sea Trout", Legendary, Shallow, 3, 11, 60, 95, 150),
        Species(10, LaneSeed.Silkeborg, "Perch", Common, Deep, 0.3, 1.6, 20, 45, 20),
        Species(11, LaneSeed.Silkeborg, "Pike", Rare, Deep, 2, 10, 50, 110, 60),
        Species(12, LaneSeed.Silkeborg, "Salmon", Legendary, Deep, 5, 18, 75, 120, 180),

        Species(13, LaneSeed.Ringkobing, "Flounder", Common, Shallow, 0.3, 1.5, 20, 45, 25),
        Species(14, LaneSeed.Ringkobing, "Garfish", Rare, Shallow, 0.4, 1.3, 50, 90, 70),
        Species(15, LaneSeed.Ringkobing, "Turbot", Legendary, Shallow, 2, 12, 40, 80, 220),
        Species(16, LaneSeed.Ringkobing, "Herring", Common, Deep, 0.1, 0.4, 18, 32, 28),
        Species(17, LaneSeed.Ringkobing, "Sea Bass", Rare, Deep, 1, 6, 40, 80, 80),
        Species(18, LaneSeed.Ringkobing, "Conger Eel", Legendary, Deep, 8, 35, 120, 220, 250),

        Species(19, LaneSeed.Vejle, "Mackerel", Common, Shallow, 0.3, 1.2, 25, 45, 35),
        Species(20, LaneSeed.Vejle, "Sea Trout", Rare, Shallow, 1.5, 8, 45, 85, 100),
        Species(21, LaneSeed.Vejle, "Swordfish", Legendary, Shallow, 30, 120, 150, 280, 320),
        Species(22, LaneSeed.Vejle, "Cod", Common, Deep, 0.8, 8, 35, 90, 40),
        Species(23, LaneSeed.Vejle, "Ling", Rare, Deep, 3, 18, 70, 150, 120),
        Species(24, LaneSeed.Vejle, "Bluefin Tuna", Legendary, Deep, 80, 350, 160, 290, 400),
    ];

    private static FishSpecies Species(
        int id, int laneId, string name, Rarity rarity, WaterDepth depth,
        double minWeightKg, double maxWeightKg, double minLengthCm, double maxLengthCm, int price)
    {
        return new FishSpecies
        {
            Id = id,
            LaneId = laneId,
            Name = name,
            Rarity = rarity,
            Depth = depth,
            MinWeightKg = minWeightKg,
            MaxWeightKg = maxWeightKg,
            MinLengthCm = minLengthCm,
            MaxLengthCm = maxLengthCm,
            Price = price,
        };
    }
}
