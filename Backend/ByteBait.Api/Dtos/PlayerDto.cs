using ByteBait.Data.Entities;

namespace ByteBait.Api.Dtos;

// The logged-in player, returned by register, login and GET /api/account/me (see docs/api-contract.md)
public record PlayerDto(Guid Id, string UserName, IEnumerable<string> Roles, int Money)
{
    public static PlayerDto From(Player player, IEnumerable<string> roles) => new(
        player.Id,
        player.UserName!,
        roles,
        player.Money);
}
