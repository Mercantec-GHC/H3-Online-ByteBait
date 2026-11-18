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

    private sealed class FakeOpenMeteo : HttpMessageHandler
    {
        private string _body = "{}";
        private HttpStatusCode _status = HttpStatusCode.OK;
        private Exception? _exception;

        public List<Uri> Requests { get; } = [];

        public void RespondWith(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
            _exception = null;
        }

        public void ThrowOnRequest(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests) Requests.Add(request.RequestUri!);
            if (_exception is not null) throw _exception;

            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class FakeTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
