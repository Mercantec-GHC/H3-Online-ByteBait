namespace ByteBait.Data.Entities;

// Equipment a player owns. The best item of each type is the one in use.
public class PlayerEquipment
{
    public Guid PlayerId { get; set; }

    public Player Player { get; set; } = null!;

    public int EquipmentId { get; set; }

    public Equipment Equipment { get; set; } = null!;

    // Set by the database, UTC
    public DateTime PurchasedAt { get; set; }
}
