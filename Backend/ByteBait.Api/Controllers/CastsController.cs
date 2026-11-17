using ByteBait.Api.Dtos;
using ByteBait.Data;
using ByteBait.Data.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ByteBait.Api.Controllers;

[ApiController]
[Route("api/casts")]
public class CastsController(ByteBaitDbContext db) : ControllerBase
{
    // Saves a cast from the game. [ApiController] returns a 400 ProblemDetails if the body is invalid.
    [HttpPost]
    public async Task<ActionResult<CastDto>> Save(SaveCastDto dto)
    {
        var cast = new Cast
        {
            Zone = Enum.Parse<CastZone>(dto.Zone!, ignoreCase: true),
            Strength = dto.Strength!.Value,
            // Npgsql only accepts UTC in a "timestamp with time zone" column
            CastAt = dto.CastAt!.Value.UtcDateTime,
        };

        db.Casts.Add(cast);
        await db.SaveChangesAsync();

        // There is no GET endpoint for a single cast yet, so no Location header
        return Created((string?)null, CastDto.From(cast));
    }
}
