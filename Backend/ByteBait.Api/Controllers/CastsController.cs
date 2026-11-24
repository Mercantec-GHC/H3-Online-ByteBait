using ByteBait.Api.Dtos;
using ByteBait.Api.Game;
using ByteBait.Api.Players;
using ByteBait.Data;
using ByteBait.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBait.Api.Controllers;

[ApiController]
[Route("api/casts")]
public class CastsController(ByteBaitDbContext db, CurrentPlayer player, FishPicker fishPicker) : ControllerBase
{
    // Saves a cast from the game. [ApiController] returns a 400 ProblemDetails if the body is invalid.
    // A cast on a level also gets its fish here, before the fight starts, so the game can't choose it (S2.8).
    [HttpPost]
    public async Task<ActionResult<CastDto>> Save(SaveCastDto dto, CancellationToken cancellationToken)
    {
        var cast = new Cast
        {
            Zone = Enum.Parse<CastZone>(dto.Zone!, ignoreCase: true),
            Strength = dto.Strength!.Value,
            // Npgsql only accepts UTC in a "timestamp with time zone" column
            CastAt = dto.CastAt!.Value.UtcDateTime,
            PlayerId = player.Id,
        };

        if (dto.LevelId is { } levelId)
        {
            var level = await db.Levels.AsNoTracking().SingleOrDefaultAsync(l => l.Id == levelId, cancellationToken);
            if (level is null)
            {
                ModelState.AddModelError("levelId", $"There is no level with id {levelId}.");
                return ValidationProblem(ModelState);
            }

            cast.LevelId = level.Id;

            if (await PickFishAsync(level.LaneId, cast, cancellationToken) is { } fish)
            {
                cast.FishSpecies = fish.Species;
                cast.FishSpeciesId = fish.Species.Id;
                cast.WeightKg = fish.WeightKg;
                cast.LengthCm = fish.LengthCm;
            }
        }

        db.Casts.Add(cast);
        await db.SaveChangesAsync(cancellationToken);

        // There is no GET endpoint for a single cast yet, so no Location header
        return Created((string?)null, CastDto.From(cast));
    }

    // The chances and multipliers are read on every cast, so a change in the admin panel counts straight away
    private async Task<PickedFish?> PickFishAsync(int laneId, Cast cast, CancellationToken cancellationToken)
    {
        var species = await db.FishSpecies.Where(s => s.LaneId == laneId).ToListAsync(cancellationToken);
        var baseChances = await db.RarityChances.ToDictionaryAsync(r => r.Rarity, r => r.BaseChance, cancellationToken);
        var multiplier = await db.ZoneMultipliers
            .Where(z => z.Zone == cast.Zone)
            .Select(z => (double?)z.Multiplier)
            .SingleOrDefaultAsync(cancellationToken) ?? 1.0;

        return fishPicker.Pick(species, cast.Strength, baseChances, multiplier);
    }
}
