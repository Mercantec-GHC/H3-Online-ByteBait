using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ByteBait.Api.Weather;

// Current weather from Open-Meteo, in the units we ask for (°C, m/s, mm).
public record CurrentWeather(
    double Temperature,
    double WindSpeed,
    double WindGusts,
    double Precipitation,
    int WeatherCode,
    bool IsDay,
    DateTime ObservedAt);

// Thrown when Open-Meteo doesn't answer, answers with an error, or sends something we can't read.
public class OpenMeteoException(string message, Exception? inner = null) : Exception(message, inner);

// Fetches the current weather from Open-Meteo (free, no API key). Same idea as OpenMeteoClient in Arduino-meteo.
public class OpenMeteoClient(IHttpClientFactory httpClientFactory)
{
    public const string HttpClientName = "open-meteo";

    public async Task<CurrentWeather> GetCurrentAsync(double latitude, double longitude, CancellationToken cancellationToken)
    {
        // InvariantCulture, so the coordinates are written as 56.09 and not 56,09 on a Danish machine.
        // timezone=GMT gives the time in UTC.
        var coordinates = string.Create(CultureInfo.InvariantCulture, $"latitude={latitude}&longitude={longitude}");
        var path = $"v1/forecast?{coordinates}" +
                   "&current=temperature_2m,precipitation,weather_code,wind_speed_10m,wind_gusts_10m,is_day" +
                   "&wind_speed_unit=ms&timezone=GMT";

        var http = httpClientFactory.CreateClient(HttpClientName);

        try
        {
            using var response = await http.GetAsync(path, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new OpenMeteoException($"Open-Meteo answered {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadFromJsonAsync<OpenMeteoResponse>(cancellationToken);
            var current = body?.Current ?? throw new OpenMeteoException("Open-Meteo sent no current weather.");

            return new CurrentWeather(
                current.Temperature,
                current.WindSpeed,
                current.WindGusts,
                current.Precipitation,
                current.WeatherCode,
                current.IsDay == 1,
                DateTime.SpecifyKind(current.Time, DateTimeKind.Utc));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // TaskCanceledException without our own cancellation means the HttpClient timeout was hit
            throw new OpenMeteoException("Open-Meteo did not answer.", ex);
        }
    }

    private record OpenMeteoResponse([property: JsonPropertyName("current")] OpenMeteoCurrent? Current);

    private record OpenMeteoCurrent(
        [property: JsonPropertyName("time")] DateTime Time,
        [property: JsonPropertyName("temperature_2m")] double Temperature,
        [property: JsonPropertyName("precipitation")] double Precipitation,
        [property: JsonPropertyName("weather_code")] int WeatherCode,
        [property: JsonPropertyName("wind_speed_10m")] double WindSpeed,
        [property: JsonPropertyName("wind_gusts_10m")] double WindGusts,
        [property: JsonPropertyName("is_day")] int IsDay);
}
