namespace ByteBait.Api.Dtos;

// Response body for GET /api/account/antiforgery (see docs/api-contract.md)
public record AntiforgeryTokenDto(string Token);
