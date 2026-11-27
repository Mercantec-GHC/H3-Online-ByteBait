using ByteBait.Api.Dtos;
using ByteBait.Data;
using ByteBait.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ByteBait.Api.Controllers;

// Create, edit and delete fish species in the admin panel (S2.11). Only for the admin role.
[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/species")]
public class AdminSpeciesController(ByteBaitDbContext db) : ControllerBase
{
    // The title of the 409 for a form opened before someone else saved. The admin panel offers to open it again.
    public const string SpeciesChangedTitle = "Species has changed";

    // Sorted like the admin panel shows them: by lane, then depth, then rarity
    [HttpGet]
    public async Task<IEnumerable<FishSpeciesDto>> List(CancellationToken cancellationToken)
    {
        var species = await db.FishSpecies.AsNoTracking().ToListAsync(cancellationToken);

        // The enums are stored as text, so the database would sort them alphabetically
        return species
            .OrderBy(s => s.LaneId).ThenBy(s => s.Depth).ThenBy(s => s.Rarity).ThenBy(s => s.Name)
            .Select(FishSpeciesDto.From);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FishSpeciesDto>> Get(int id, CancellationToken cancellationToken)
    {
        var species = await db.FishSpecies.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        return species is null ? SpeciesNotFound(id) : FishSpeciesDto.From(species);
    }

    [HttpPost]
    public async Task<ActionResult<FishSpeciesDto>> Create(SaveFishSpeciesDto dto, CancellationToken cancellationToken)
    {
        if (!await LaneExistsAsync(dto.LaneId!.Value, cancellationToken))
        {
            return LaneNotFound(dto.LaneId.Value);
        }

        var species = new FishSpecies { Name = dto.Name! };
        dto.CopyTo(species);
        db.FishSpecies.Add(species);

        if (await SaveAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return CreatedAtAction(nameof(Get), new { id = species.Id }, FishSpeciesDto.From(species));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<FishSpeciesDto>> Update(int id, SaveFishSpeciesDto dto, CancellationToken cancellationToken)
    {
        if (dto.Version is not { } version)
        {
            ModelState.AddModelError("version", "Send the version of the species you opened.");
            return ValidationProblem(ModelState);
        }

        var species = await db.FishSpecies.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (species is null)
        {
            return SpeciesNotFound(id);
        }

        if (!await LaneExistsAsync(dto.LaneId!.Value, cancellationToken))
        {
            return LaneNotFound(dto.LaneId.Value);
        }

        // Saved as "UPDATE ... WHERE xmin = <version>", so a form opened before another admin saved changes nothing
        db.Entry(species).Property(s => s.Version).OriginalValue = version;

        // Old casts were made on a level on this lane, so the species can't move away from them
        if (dto.LaneId != species.LaneId && await db.Casts.AnyAsync(c => c.FishSpeciesId == id, cancellationToken))
        {
            return SpeciesHasBeenFished(
                $"Species {id} has been cast for on its lane, so it can't move to another lane. Create it on the other lane instead.");
        }

        // Fish that are already caught keep their weight and length, and old sales keep their price (sold_for)
        dto.CopyTo(species);

        if (await SaveAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return FishSpeciesDto.From(species);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var species = await db.FishSpecies.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (species is null)
        {
            return SpeciesNotFound(id);
        }

        db.FishSpecies.Remove(species);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // The catches and statistics point to it, so the database won't let it go (docs/data-model.md)
            return SpeciesHasBeenFished($"Species {id} has been cast for, so it can't be deleted. Change its rarity or price instead.");
        }

        return NoContent();
    }

    // Null when it was saved. The unique index is the only check, so two admins can't create the same species at once.
    private async Task<ObjectResult?> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: SpeciesChangedTitle,
                detail: "Another admin saved this species after you opened it. Open it again to see their change.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Species already exists on the lane",
                detail: "Another species on the same lane has that name.");
        }
    }

    private Task<bool> LaneExistsAsync(int laneId, CancellationToken cancellationToken) =>
        db.Lanes.AnyAsync(l => l.Id == laneId, cancellationToken);

    private ActionResult LaneNotFound(int laneId)
    {
        ModelState.AddModelError("laneId", $"There is no lane with id {laneId}.");
        return ValidationProblem(ModelState);
    }

    private ObjectResult SpeciesHasBeenFished(string detail) => Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Species has been fished",
        detail: detail);

    private ObjectResult SpeciesNotFound(int id) => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Species not found",
        detail: $"There is no species with id {id}.");
}
