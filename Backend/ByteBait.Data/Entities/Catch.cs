namespace ByteBait.Data.Entities;

// The end of the fight for a cast. The fish itself (species, weight, length) is on the cast,
// because the server picks it when the cast is saved.
// A caught fish is either kept, sold in the hub or moved to the aquarium.
public class Catch
{
    public int Id { get; set; }

    public int CastId { get; set; }

    public Cast Cast { get; set; } = null!;

    public CatchResult Result { get; set; }

    // Set by the database, UTC
    public DateTime ResolvedAt { get; set; }

    public DateTime? SoldAt { get; set; }

    // The price at the time of the sale, since the admin can change prices later
    public int? SoldFor { get; set; }

    public bool InAquarium { get; set; }
}
