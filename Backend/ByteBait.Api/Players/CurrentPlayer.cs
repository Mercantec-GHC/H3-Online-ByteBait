namespace ByteBait.Api.Players;

// The player who is logged in, used by the game endpoints (levels, casts, catches).
// Spil-endpoints never take a player id from the request (see docs/api-contract.md, "Login").
//
// TODO(#76): Until player accounts exist, Id is null: casts and catches are saved without a player,
// and progress is counted over everything saved without a player. When S2.13 adds Identity and the
// login cookie, read the user's id from HttpContext.User here and require login on the game endpoints.
// Nothing else in the game endpoints has to change.
public class CurrentPlayer
{
    public Guid? Id => null;
}
