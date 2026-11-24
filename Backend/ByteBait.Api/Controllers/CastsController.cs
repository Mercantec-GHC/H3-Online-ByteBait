using ByteBait.Api.Dtos;
using ByteBait.Api.Players;
using ByteBait.Data;
using ByteBait.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBait.Api.Controllers;

[ApiController]
[Route("api/casts")]
public class CastsController(ByteBaitDbContext db, CurrentPlayer player) : ControllerBase
{
    // Saves a cast from the game. [ApiController] returns a 400 ProblemDetails if the body is invalid.
    [HttpPost]
    public async Task<ActionResult<CastDto>> Save(SaveCastDto dto, CancellationToken cancellationToken)
    {
        if (dto.LevelId is { } levelId && !await db.Levels.AnyAsync(l => l.Id == levelId, cancellationToken))
        {
            ModelState.AddModelError("levelId", $"There is no level with id {levelId}.");
            return ValidationProblem(ModelState);
        }

        var cast = new Cast
        {
            Zone = Enum.Parse<CastZone>(dto.Zone!, ignoreCase: true),
            Strength = dto.Strength!.Value,
            // Npgsql only accepts UTC in a "timestamp with time zone" column
            CastAt = dto.CastAt!.Value.UtcDateTime,
            LevelId = dto.LevelId,
            PlayerId = player.Id,
        };

        db.Casts.Add(cast);
        await db.SaveChangesAsync(cancellationToken);

        // There is no GET endpoint for a single cast yet, so no Location header
        return Created((string?)null, CastDto.From(cast));
    }
}
