namespace ByteBait.Api.Weather;

public record WeatherLane(int LaneId, string City, double Latitude, double Longitude);

// The four lanes and the town each lane gets its weather from (see the product description).
// Fixed in code for now. When the lanes table from S1.11 exists, the coordinates can move there.
public static class WeatherLanes
{
    public static readonly IReadOnlyList<WeatherLane> All =
    [
        new(1, "Nordborg", 55.06, 9.74),
        new(2, "Silkeborg", 56.17, 9.55),
        new(3, "Ringkøbing", 56.09, 8.24),
        new(4, "Vejle", 55.71, 9.54),
    ];

    public static WeatherLane? Find(int laneId) => All.FirstOrDefault(l => l.LaneId == laneId);
}
