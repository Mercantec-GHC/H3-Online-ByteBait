using ByteBait.Data.Entities;

namespace ByteBait.Data.Seed;

// One of each type to begin with. The bait is cheap enough that everyone can buy it after the first level,
// even with five of the cheapest fish.
internal static class EquipmentSeed
{
    public static readonly IReadOnlyList<Equipment> All =
    [
        new() { Id = 1, Type = EquipmentType.Rod, Name = "Carbon Rod", Price = 200, Power = 1 },
        new() { Id = 2, Type = EquipmentType.Bait, Name = "Nightcrawler", Price = 50, Power = 1 },
        new() { Id = 3, Type = EquipmentType.Line, Name = "Braided Line", Price = 150, Power = 1 },
    ];
}
