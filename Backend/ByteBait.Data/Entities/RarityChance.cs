namespace ByteBait.Data.Entities;

// Base chance of getting a rare or legendary fish. Multiplied by the cast zone's multiplier.
// Common fish have no row: their chance is what is left, so the chances always add up to 100 %.
public class RarityChance
{
    public Rarity Rarity { get; set; }

    // 0.20 = 20 %
    public double BaseChance { get; set; }
}
