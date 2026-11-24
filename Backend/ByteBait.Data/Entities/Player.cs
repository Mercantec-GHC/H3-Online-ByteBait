namespace ByteBait.Data.Entities;

// A player account. In S2.13 this becomes the ASP.NET Core Identity user on the same table,
// so casts, money and the aquarium keep pointing at the same Id.
public class Player
{
    public Guid Id { get; set; }

    public required string UserName { get; set; }

    public int Money { get; set; }

    // Set when the player shares the aquarium, used in the link others can open
    public string? AquariumShareCode { get; set; }

    // Set by the database, UTC
    public DateTime CreatedAt { get; set; }

    public List<PlayerEquipment> Equipment { get; } = [];

    public List<Cast> Casts { get; } = [];
}
