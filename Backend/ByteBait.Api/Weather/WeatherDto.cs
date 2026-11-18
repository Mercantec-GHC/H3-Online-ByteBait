namespace ByteBait.Api.Weather;

// Response body for GET /api/lanes/{laneId}/weather (see docs/api-contract.md).
// Raw weather only. The effects on the game (smaller green zone, more bites, ...) come in S3.4.
public record WeatherDto(
    int LaneId,
    string City,
    double Temperature,
    double WindSpeed,
    double WindGusts,
    double Precipitation,
    int WeatherCode,
    bool IsDay,
    DateTime ObservedAt,
    bool Stale)
{
    public static WeatherDto From(LaneWeather result) => new(
        result.Lane.LaneId,
        result.Lane.City,
        result.Weather.Temperature,
        result.Weather.WindSpeed,
        result.Weather.WindGusts,
        result.Weather.Precipitation,
        result.Weather.WeatherCode,
        result.Weather.IsDay,
        result.Weather.ObservedAt,
        result.Stale);
}
