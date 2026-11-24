using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ByteBait.Api.Weather;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ByteBait.Api.Tests;

// Open-Meteo is replaced by a fake handler, so the tests never call the real service.
// Each test gets its own host, so the weather cache starts empty.
public class WeatherEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string OpenMeteoJson = """
        {
          "current": {
            "time": "2026-11-18T10:15",
            "interval": 900,
            "temperature_2m": 7.4,
            "precipitation": 0.3,
            "weather_code": 61,
            "wind_speed_10m": 9.2,
            "wind_gusts_10m": 15.1,
            "is_day": 1
          }
        }
        """;

    private readonly FakeOpenMeteo _openMeteo = new();
    private readonly FakeTime _time = new();

    private HttpClient CreateClient() => factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient(OpenMeteoClient.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => _openMeteo);
            services.AddSingleton<TimeProvider>(_time);
        })).CreateClient();

    [Fact]
    public async Task Lane_returns_the_weather_in_its_town()
    {
        _openMeteo.RespondWith(OpenMeteoJson);
        var client = CreateClient();

        var response = await client.GetAsync("/api/lanes/3/weather");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.GetProperty("laneId").GetInt32());
        Assert.Equal("Ringkøbing", body.GetProperty("city").GetString());
        Assert.Equal(7.4, body.GetProperty("temperature").GetDouble());
        Assert.Equal(9.2, body.GetProperty("windSpeed").GetDouble());
        Assert.Equal(15.1, body.GetProperty("windGusts").GetDouble());
        Assert.Equal(0.3, body.GetProperty("precipitation").GetDouble());
        Assert.Equal(61, body.GetProperty("weatherCode").GetInt32());
        Assert.True(body.GetProperty("isDay").GetBoolean());
        Assert.Equal("2026-11-18T10:15:00Z", body.GetProperty("observedAt").GetString());
        Assert.False(body.GetProperty("stale").GetBoolean());

        // Ringkøbing's coordinates with a dot, wind in m/s and the time in UTC
        var query = _openMeteo.Requests.Single().Query;
        Assert.Contains("latitude=56.09&longitude=8.24", query);
        Assert.Contains("wind_speed_unit=ms", query);
        Assert.Contains("timezone=GMT", query);
    }

    [Fact]
    public async Task Weather_is_cached_per_town_for_10_minutes()
    {
        _openMeteo.RespondWith(OpenMeteoJson);
        var client = CreateClient();

        await client.GetAsync("/api/lanes/1/weather");
        _time.Advance(TimeSpan.FromMinutes(9));
        await client.GetAsync("/api/lanes/1/weather");
        Assert.Single(_openMeteo.Requests);

        // Another town has its own cache
        await client.GetAsync("/api/lanes/2/weather");
        Assert.Equal(2, _openMeteo.Requests.Count);

        // After 10 minutes the weather is fetched again
        _time.Advance(TimeSpan.FromMinutes(1));
        await client.GetAsync("/api/lanes/1/weather");
        Assert.Equal(3, _openMeteo.Requests.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task Unknown_lane_returns_404(int laneId)
    {
        var client = CreateClient();

        var response = await client.GetAsync($"/api/lanes/{laneId}/weather");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(_openMeteo.Requests);
    }

    [Fact]
    public async Task Open_Meteo_error_returns_the_last_weather_as_stale()
    {
        _openMeteo.RespondWith(OpenMeteoJson);
        var client = CreateClient();
        await client.GetAsync("/api/lanes/4/weather");

        _time.Advance(TimeSpan.FromMinutes(15));
        _openMeteo.RespondWith("""{"error":true,"reason":"Server error"}""", HttpStatusCode.InternalServerError);
        var response = await client.GetAsync("/api/lanes/4/weather");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("stale").GetBoolean());
        Assert.Equal(7.4, body.GetProperty("temperature").GetDouble());
    }

    [Fact]
    public async Task Open_Meteo_error_without_earlier_weather_returns_503()
    {
        _openMeteo.RespondWith("""{"error":true,"reason":"Server error"}""", HttpStatusCode.InternalServerError);
        var client = CreateClient();

        var response = await client.GetAsync("/api/lanes/1/weather");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Open_Meteo_timeout_without_earlier_weather_returns_503()
    {
        // This is what HttpClient throws when its timeout is hit
        _openMeteo.ThrowOnRequest(new TaskCanceledException("Timeout", new TimeoutException()));
        var client = CreateClient();

        var response = await client.GetAsync("/api/lanes/1/weather");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Requests_at_the_same_time_call_Open_Meteo_only_once()
    {
        _openMeteo.RespondWith(OpenMeteoJson);
        _openMeteo.HoldResponses();
        var client = CreateClient();

        var requests = Enumerable.Range(0, 20).Select(_ => client.GetAsync("/api/lanes/2/weather")).ToList();

        // Give all 20 requests time to reach the service while the first one waits for Open-Meteo
        await _openMeteo.FirstRequest.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(300);
        _openMeteo.ReleaseResponses();

        var responses = await Task.WhenAll(requests);
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Single(_openMeteo.Requests);
    }

    [Fact]
    public async Task After_an_error_Open_Meteo_is_not_called_again_for_1_minute()
    {
        _openMeteo.RespondWith("""{"error":true,"reason":"Server error"}""", HttpStatusCode.InternalServerError);
        var client = CreateClient();

        var first = await client.GetAsync("/api/lanes/1/weather");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode);

        // Within the minute the answer comes straight away, without calling Open-Meteo
        _time.Advance(TimeSpan.FromSeconds(59));
        var second = await client.GetAsync("/api/lanes/1/weather");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, second.StatusCode);
        Assert.Single(_openMeteo.Requests);

        // After the minute Open-Meteo is tried again
        _openMeteo.RespondWith(OpenMeteoJson);
        _time.Advance(TimeSpan.FromSeconds(1));
        var third = await client.GetAsync("/api/lanes/1/weather");
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        Assert.Equal(2, _openMeteo.Requests.Count);
    }

    [Fact]
    public async Task After_an_error_the_stale_weather_is_kept_for_1_minute_without_calling_Open_Meteo()
    {
        _openMeteo.RespondWith(OpenMeteoJson);
        var client = CreateClient();
        await client.GetAsync("/api/lanes/4/weather");

        _time.Advance(TimeSpan.FromMinutes(15));
        _openMeteo.RespondWith("""{"error":true,"reason":"Server error"}""", HttpStatusCode.InternalServerError);
        await client.GetAsync("/api/lanes/4/weather");
        Assert.Equal(2, _openMeteo.Requests.Count);

        _time.Advance(TimeSpan.FromSeconds(30));
        var response = await client.GetAsync("/api/lanes/4/weather");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("stale").GetBoolean());
        Assert.Equal(2, _openMeteo.Requests.Count);
    }

    private sealed class FakeOpenMeteo : HttpMessageHandler
    {
        private string _body = "{}";
        private HttpStatusCode _status = HttpStatusCode.OK;
        private Exception? _exception;
        private TaskCompletionSource? _hold;
        private readonly TaskCompletionSource _firstRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<Uri> Requests { get; } = [];

        // Completes when the first request reaches Open-Meteo
        public Task FirstRequest => _firstRequest.Task;

        public void RespondWith(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
            _exception = null;
        }

        public void ThrowOnRequest(Exception exception) => _exception = exception;

        // Open-Meteo doesn't answer until ReleaseResponses is called, like a slow Open-Meteo
        public void HoldResponses() => _hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ReleaseResponses() => _hold?.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests) Requests.Add(request.RequestUri!);
            _firstRequest.TrySetResult();
            if (_hold is not null) await _hold.Task.WaitAsync(cancellationToken);
            if (_exception is not null) throw _exception;

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class FakeTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
