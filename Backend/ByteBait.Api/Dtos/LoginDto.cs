using System.ComponentModel.DataAnnotations;

namespace ByteBait.Api.Dtos;

// Request body for POST /api/account/login (see docs/api-contract.md).
// Only the upper limits are checked. A name or password that breaks the other rules is just wrong.
public class LoginDto
{
    [Required]
    [StringLength(32, ErrorMessage = "Username can't be longer than 32 characters.")]
    public string? UserName { get; set; }

    [Required]
    [StringLength(128, ErrorMessage = "Password can't be longer than 128 characters.")]
    public string? Password { get; set; }
}
