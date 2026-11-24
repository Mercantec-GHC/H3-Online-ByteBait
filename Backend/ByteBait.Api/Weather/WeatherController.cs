using Microsoft.AspNetCore.Mvc;

namespace ByteBait.Api.Weather;

[ApiController]
[Route("api/lanes")]
public class WeatherController(WeatherService weatherService) : ControllerBase
{
    // The current weather in the lane's town. Login is added when S2.13 (player accounts) is done.
    [HttpGet("{laneId:int}/weather")]
    public async Task<ActionResult<WeatherDto>> Get(int laneId, CancellationToken cancellationToken)
    {
        var lane = WeatherLanes.Find(laneId);
        if (lane is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Lane not found",
                detail: $"There is no lane with id {laneId}. Lanes are 1-4.");
        }

        var result = await weatherService.GetAsync(lane, cancellationToken);
        if (result is null)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Weather is not available",
                detail: $"Open-Meteo did not answer, and there is no earlier weather for {lane.City}.");
        }

        return WeatherDto.From(result);
    }
}
