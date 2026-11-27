using ByteBait.Api.Dtos;
using ByteBait.Data;
using ByteBait.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBait.Api.Controllers;

// The lanes, so the admin panel can choose one for a species (S2.11)
[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/lanes")]
public class AdminLanesController(ByteBaitDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<AdminLaneDto>> List(CancellationToken cancellationToken) =>
        await db.Lanes
            .AsNoTracking()
            .OrderBy(l => l.Number)
            .Select(l => new AdminLaneDto(l.Id, l.Number, l.Name))
            .ToListAsync(cancellationToken);
}
