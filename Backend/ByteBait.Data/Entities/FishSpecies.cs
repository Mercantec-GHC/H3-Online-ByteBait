namespace ByteBait.Data.Entities;

// A kind of fish, e.g. pike. Rarity and price can be changed in the admin panel.
// The weight and length of a caught fish are picked between the min and max values.
public class FishSpecies
{
    public int Id { get; set; }

    public int LaneId { get; set; }

    public Lane Lane { get; set; } = null!;

    public required string Name { get; set; }

    public Rarity Rarity { get; set; }

    public WaterDepth Depth { get; set; }

    public double MinWeightKg { get; set; }

    public double MaxWeightKg { get; set; }

    public double MinLengthCm { get; set; }

    public double MaxLengthCm { get; set; }

    // What the hub pays for the fish
    public int Price { get; set; }
}
