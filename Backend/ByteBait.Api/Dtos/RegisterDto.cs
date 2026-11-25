using System.ComponentModel.DataAnnotations;

namespace ByteBait.Api.Dtos;

// Request body for POST /api/account/register (see docs/api-contract.md).
// The allowed characters and the shortest password are set in PlayerAccountExtensions, where Identity checks them.
public class RegisterDto
{
    [Required]
    [StringLength(32, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 32 characters.")]
    public string? UserName { get; set; }

    [Required]
    [StringLength(128, ErrorMessage = "Password can't be longer than 128 characters.")]
    public string? Password { get; set; }
}
