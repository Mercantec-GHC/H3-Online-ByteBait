using System.Collections.Concurrent;

namespace ByteBait.Api.Weather;

// Weather for one lane. Stale = Open-Meteo didn't answer, so this is the last weather we got.
public record LaneWeather(WeatherLane Lane, CurrentWeather Weather, bool Stale);

// Gives each lane the current weather in its town without spamming Open-Meteo:
// - The weather is cached per town for 10 minutes, so at most 4 calls to Open-Meteo every 10 minutes.
// - If Open-Meteo fails, the last weather we got is returned with Stale = true, so the game can go on.
// - Only if we have never got any weather for the town is the result null (the endpoint answers 503).
// Registered as a singleton, so the cache is shared by all requests.
public class WeatherService(OpenMeteoClient openMeteo, TimeProvider time, ILogger<WeatherService> logger)
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private record CacheEntry(CurrentWeather Weather, DateTimeOffset FetchedAt);

    private readonly ConcurrentDictionary<int, CacheEntry> _cache = new();

    public async Task<LaneWeather?> GetAsync(WeatherLane lane, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        if (_cache.TryGetValue(lane.LaneId, out var cached) && now - cached.FetchedAt < CacheDuration)
        {
            return new LaneWeather(lane, cached.Weather, Stale: false);
        }

        try
        {
            var weather = await openMeteo.GetCurrentAsync(lane.Latitude, lane.Longitude, cancellationToken);
            _cache[lane.LaneId] = new CacheEntry(weather, now);
            return new LaneWeather(lane, weather, Stale: false);
        }
        catch (OpenMeteoException ex)
        {
            if (cached is not null)
            {
                logger.LogWarning(ex, "Open-Meteo failed for {City}, using weather from {FetchedAt}",
                    lane.City, cached.FetchedAt);
                return new LaneWeather(lane, cached.Weather, Stale: true);
            }

            logger.LogError(ex, "Open-Meteo failed for {City}, and there is no earlier weather", lane.City);
            return null;
        }
    }
}
