namespace ByteBait.Data.Entities;

// A cast from the Oplà, saved through POST /api/casts (see docs/api-contract.md).
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
}
