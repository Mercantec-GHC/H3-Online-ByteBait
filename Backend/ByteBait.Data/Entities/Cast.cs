namespace ByteBait.Data.Entities;

// A cast from the Oplà, saved through POST /api/casts (see docs/api-contract.md).
// From sprint 2 the server also picks the fish here, before the fight starts, so the game can't cheat.
public class Cast
{
    public int Id { get; set; }

    public CastZone Zone { get; set; }

    // 0.0 = softest cast, 1.0 = hardest
    public double Strength { get; set; }

    // Sent by the game, UTC
    public DateTime CastAt { get; set; }

    // Set by the database when the row is inserted, UTC
    public DateTime CreatedAt { get; set; }

    // The fields below are empty for casts from sprint 1, where there is no login or level yet

    public Guid? PlayerId { get; set; }

    public Player? Player { get; set; }

    public int? LevelId { get; set; }

    public Level? Level { get; set; }

    public int? FishSpeciesId { get; set; }

    public FishSpecies? FishSpecies { get; set; }

    public double? WeightKg { get; set; }

    public double? LengthCm { get; set; }

    // Empty until the fight is over
    public Catch? Catch { get; set; }
}
