using ByteBait.Api.Dtos;
using ByteBait.Api.Players;
using ByteBait.Data;
using ByteBait.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ByteBait.Api.Controllers;

[ApiController]
[Route("api/catches")]
public class CatchesController(ByteBaitDbContext db, CurrentPlayer player) : ControllerBase
{
    // Saves how the fight for a cast ended. One catch per cast, so the same fight can't be saved twice.
    [HttpPost]
    public async Task<ActionResult<CatchDto>> Save(SaveCatchDto dto, CancellationToken cancellationToken)
    {
        var castId = dto.CastId!.Value;

        // Only the player's own casts. Until #76 that is the casts saved without a player.
        var cast = await db.Casts
            .Include(c => c.Catch)
            .SingleOrDefaultAsync(c => c.Id == castId && c.PlayerId == player.Id, cancellationToken);

        if (cast is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Cast not found",
                detail: $"There is no cast with id {castId}.");
        }

        if (cast.LevelId is not { } levelId)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Cast has no level",
                detail: "Only a cast made on a level can end in a catch. Send levelId with POST /api/casts.");
        }

        if (cast.Catch is not null)
        {
            return CatchAlreadySaved(castId);
        }

        var saved = new Catch
        {
            CastId = castId,
            Result = Enum.Parse<CatchResult>(dto.Result!, ignoreCase: true),
        };
        db.Catches.Add(saved);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two requests for the same cast at the same time: the unique index on cast_id stopped the second
            return CatchAlreadySaved(castId);
        }

        var progress = await LevelProgressDto.LoadAsync(db, levelId, player.Id, cancellationToken);

        // There is no GET endpoint for a single catch yet, so no Location header
        return Created((string?)null, CatchDto.From(saved, progress!));
    }

    private ObjectResult CatchAlreadySaved(int castId) => Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Catch already saved",
        detail: $"The fight for cast {castId} has already been saved.");
}
