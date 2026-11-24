using System.Collections.Concurrent;

namespace ByteBait.Api.Weather;

// Weather for one lane. Stale = Open-Meteo didn't answer, so this is the last weather we got.
public record LaneWeather(WeatherLane Lane, CurrentWeather Weather, bool Stale);

// Gives each lane the current weather in its town without spamming Open-Meteo:
// - The weather is cached per town for 10 minutes, so at most 4 calls to Open-Meteo every 10 minutes.
// - Only one request per town calls Open-Meteo at a time. Requests that arrive meanwhile wait and use its result.
// - If Open-Meteo fails, the last weather we got is returned with Stale = true, so the game can go on.
//   Only if we have never got any weather for the town is the result null (the endpoint answers 503).
// - After a failure we wait 1 minute before calling Open-Meteo again, so a slow or broken Open-Meteo
//   doesn't make every request wait for the timeout.
// Registered as a singleton, so the cache is shared by all requests.
public class WeatherService(OpenMeteoClient openMeteo, TimeProvider time, ILogger<WeatherService> logger)
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    private record CacheEntry(CurrentWeather Weather, DateTimeOffset FetchedAt);

    // Last weather we got (null if never) and when Open-Meteo last failed (null if the last call worked)
    private record TownState(CacheEntry? Last, DateTimeOffset? FailedAt);

    private readonly ConcurrentDictionary<int, TownState> _towns = new();

    private readonly ConcurrentDictionary<int, SemaphoreSlim> _fetchLocks = new();

    public async Task<LaneWeather?> GetAsync(WeatherLane lane, CancellationToken cancellationToken)
    {
        if (TryAnswerWithoutFetching(lane, out var answer))
        {
            return answer;
        }

        var fetchLock = _fetchLocks.GetOrAdd(lane.LaneId, _ => new SemaphoreSlim(1, 1));
        await fetchLock.WaitAsync(cancellationToken);
        try
        {
            // Another request may have fetched (or failed) while this one waited
            if (TryAnswerWithoutFetching(lane, out answer))
            {
                return answer;
            }

            return await FetchAsync(lane, cancellationToken);
        }
        finally
        {
            fetchLock.Release();
        }
    }

    // True when the cache can answer: the weather is fresh, or Open-Meteo failed less than RetryDelay ago.
    private bool TryAnswerWithoutFetching(WeatherLane lane, out LaneWeather? answer)
    {
        var now = time.GetUtcNow();
        _towns.TryGetValue(lane.LaneId, out var town);

        if (town?.Last is { } last && now - last.FetchedAt < CacheDuration)
        {
            answer = new LaneWeather(lane, last.Weather, Stale: false);
            return true;
        }

        if (town?.FailedAt is { } failedAt && now - failedAt < RetryDelay)
        {
            answer = town.Last is { } stale ? new LaneWeather(lane, stale.Weather, Stale: true) : null;
            return true;
        }

        answer = null;
        return false;
    }

    private async Task<LaneWeather?> FetchAsync(WeatherLane lane, CancellationToken cancellationToken)
    {
        _towns.TryGetValue(lane.LaneId, out var town);

        try
        {
            var weather = await openMeteo.GetCurrentAsync(lane.Latitude, lane.Longitude, cancellationToken);
            _towns[lane.LaneId] = new TownState(new CacheEntry(weather, time.GetUtcNow()), FailedAt: null);
            return new LaneWeather(lane, weather, Stale: false);
        }
        catch (OpenMeteoException ex)
        {
            _towns[lane.LaneId] = new TownState(town?.Last, FailedAt: time.GetUtcNow());

            if (town?.Last is { } last)
            {
                logger.LogWarning(ex, "Open-Meteo failed for {City}, using weather from {FetchedAt}",
                    lane.City, last.FetchedAt);
                return new LaneWeather(lane, last.Weather, Stale: true);
            }

            logger.LogError(ex, "Open-Meteo failed for {City}, and there is no earlier weather", lane.City);
            return null;
        }
    }
}
