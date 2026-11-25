using Microsoft.AspNetCore.Identity;

namespace ByteBait.Data.Entities;

// A player account. Identity takes care of the username, the hashed password and lockout,
// the properties here are what the game adds.
public class Player : IdentityUser<Guid>
{
    public int Money { get; set; }

    // Set when the player shares the aquarium, used in the link others can open
    public string? AquariumShareCode { get; set; }

    // Set by the database, UTC
    public DateTime CreatedAt { get; set; }

    public List<PlayerEquipment> Equipment { get; } = [];

    public List<Cast> Casts { get; } = [];
}
