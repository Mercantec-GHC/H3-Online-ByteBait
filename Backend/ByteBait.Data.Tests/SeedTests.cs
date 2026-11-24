using ByteBait.Data.Entities;
using ByteBait.Data.Seed;

namespace ByteBait.Data.Tests;

// Checks that the seed lists follow the rules of the game. Doesn't need a database.
public class SeedTests
{
    [Fact]
    public void Lane_ids_are_the_lane_numbers()
    {
        Assert.Equal([1, 2, 3, 4], LaneSeed.All.Select(l => l.Id));
        Assert.All(LaneSeed.All, l => Assert.Equal(l.Id, l.Number));
    }

    [Theory]
    [InlineData(LaneSeed.Nordborg, 10)]
    [InlineData(LaneSeed.Silkeborg, 15)]
    [InlineData(LaneSeed.Ringkobing, 20)]
    [InlineData(LaneSeed.Vejle, 25)]
    public void Lane_has_two_levels_with_the_required_fish_in_total(int laneId, int requiredFish)
    {
        var levels = LevelSeed.All.Where(l => l.LaneId == laneId).ToList();

        Assert.Equal([1, 2], levels.Select(l => l.Number));
        Assert.Equal(requiredFish, levels.Sum(l => l.RequiredFish));
    }

    [Fact]
    public void Every_cast_has_a_fish_to_pick()
    {
        var combinations =
            from lane in LaneSeed.All
            from depth in Enum.GetValues<WaterDepth>()
            from rarity in Enum.GetValues<Rarity>()
            select (LaneId: lane.Id, Depth: depth, Rarity: rarity);

        Assert.All(combinations, c => Assert.Single(FishSpeciesSeed.All,
            f => f.LaneId == c.LaneId && f.Depth == c.Depth && f.Rarity == c.Rarity));
    }

    [Fact]
    public void Species_are_worth_more_for_each_lane()
    {
        var sameKind = FishSpeciesSeed.All.GroupBy(f => (f.Depth, f.Rarity));

        Assert.All(sameKind, group =>
        {
            var prices = group.OrderBy(f => f.LaneId).Select(f => f.Price).ToList();
            // Distinct, so two lanes with the same price also fail
            Assert.Equal(prices.Distinct().Order(), prices);
        });
    }

    [Fact]
    public void Shop_sells_one_of_each_equipment_type()
    {
        Assert.Equal(Enum.GetValues<EquipmentType>(), EquipmentSeed.All.Select(e => e.Type).Order());
        Assert.All(EquipmentSeed.All, e => Assert.True(e.Price > 0 && e.Power > 0));
    }

    [Fact]
    public void Everyone_can_buy_something_after_the_first_level()
    {
        var firstLevel = LevelSeed.All.Single(l => l.LaneId == LaneSeed.Nordborg && l.Number == 1);
        var cheapestFish = FishSpeciesSeed.All.Where(f => f.LaneId == LaneSeed.Nordborg).Min(f => f.Price);
        var cheapestEquipment = EquipmentSeed.All.Min(e => e.Price);

        Assert.True(firstLevel.RequiredFish * cheapestFish >= cheapestEquipment);
    }
}
