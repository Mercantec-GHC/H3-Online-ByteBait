using ByteBait.Data.Entities;

namespace ByteBait.Data.Seed;

// 10, 15, 20 and 25 fish per lane (product description), split over the lane's two levels
internal static class LevelSeed
{
    public static readonly IReadOnlyList<Level> All =
    [
        new() { Id = 1, LaneId = LaneSeed.Nordborg, Number = 1, RequiredFish = 5 },
        new() { Id = 2, LaneId = LaneSeed.Nordborg, Number = 2, RequiredFish = 5 },
        new() { Id = 3, LaneId = LaneSeed.Silkeborg, Number = 1, RequiredFish = 7 },
        new() { Id = 4, LaneId = LaneSeed.Silkeborg, Number = 2, RequiredFish = 8 },
        new() { Id = 5, LaneId = LaneSeed.Ringkobing, Number = 1, RequiredFish = 10 },
        new() { Id = 6, LaneId = LaneSeed.Ringkobing, Number = 2, RequiredFish = 10 },
        new() { Id = 7, LaneId = LaneSeed.Vejle, Number = 1, RequiredFish = 12 },
        new() { Id = 8, LaneId = LaneSeed.Vejle, Number = 2, RequiredFish = 13 },
    ];
}
