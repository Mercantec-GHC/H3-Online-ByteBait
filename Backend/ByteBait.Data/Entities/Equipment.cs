namespace ByteBait.Data.Entities;

// A rod, bait or line sold in the hub. The starter equipment costs 0.
public class Equipment
{
    public int Id { get; set; }

    public EquipmentType Type { get; set; }

    public required string Name { get; set; }

    public int Price { get; set; }

    // How good it is. The game logic decides what it does for each type,
    // e.g. heavier fish for a rod, more rare fish for bait, fewer lost fish for a line.
    public int Power { get; set; }
}
