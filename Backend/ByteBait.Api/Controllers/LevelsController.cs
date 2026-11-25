using ByteBait.Api.Dtos;
using ByteBait.Api.Players;
using ByteBait.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBait.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/levels")]
public class LevelsController(ByteBaitDbContext db, CurrentPlayer player) : ControllerBase
{
    // Starts a level: the game gets the requirement and how many fish the player has caught on it.
    // There is no session (docs/data-model.md): the game sends the levelId with each cast instead.
    [HttpPost("{levelId:int}/start")]
    public async Task<ActionResult<LevelProgressDto>> Start(int levelId, CancellationToken cancellationToken)
    {
        var progress = await LevelProgressDto.LoadAsync(db, levelId, player.Id, cancellationToken);

        if (progress is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Level not found",
                detail: $"There is no level with id {levelId}.");
        }

        return progress;
    }
}
