namespace ByteBait.Data.Entities;

// How much the cast zone boosts the chance of rare and legendary fish (red ×0, yellow ×1, green ×1.5)
public class ZoneMultiplier
{
    public CastZone Zone { get; set; }

    public double Multiplier { get; set; }
}
