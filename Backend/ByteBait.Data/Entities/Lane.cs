namespace ByteBait.Data.Entities;

// One of the four fishing spots. Each lane is a real town, and its coordinates are used to get the weather from Open-Meteo.
public class Lane
{
    public int Id { get; set; }

    // 1–4, the order the lanes are unlocked in
    public int Number { get; set; }

    // The spot, e.g. "Nordborg Sø"
    public required string Name { get; set; }

    public required string City { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public List<Level> Levels { get; } = [];

    public List<FishSpecies> FishSpecies { get; } = [];
}
