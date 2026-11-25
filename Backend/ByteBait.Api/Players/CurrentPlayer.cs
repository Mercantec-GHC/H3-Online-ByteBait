using ByteBait.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace ByteBait.Api.Players;

// The player who is logged in, used by the game endpoints (levels, casts, catches).
// Game endpoints never take a player id from the request (see docs/api-contract.md, "Login").
// Only use it in endpoints with [Authorize], so a player is always logged in.
public class CurrentPlayer(IHttpContextAccessor httpContextAccessor, UserManager<Player> players)
{
    public Guid Id =>
        httpContextAccessor.HttpContext?.User is { } user && players.GetUserId(user) is { } id
            ? Guid.Parse(id)
            : throw new InvalidOperationException("No player is logged in. The endpoint needs [Authorize].");
}
