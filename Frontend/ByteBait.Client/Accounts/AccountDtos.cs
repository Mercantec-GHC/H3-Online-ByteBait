// The account endpoints' JSON (see docs/api-contract.md, "Konto")
using System.ComponentModel.DataAnnotations;

namespace ByteBait.Client.Accounts;

public sealed record Player(Guid Id, string UserName, string[] Roles, int Money);

// The fields of the register and login forms. The limits are the same as the API's.
public sealed class AccountForm
{
    [Required(ErrorMessage = "Enter a username.")]
    [StringLength(32, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 32 characters.")]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = "Enter a password.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 128 characters.")]
    public string Password { get; set; } = "";
}
