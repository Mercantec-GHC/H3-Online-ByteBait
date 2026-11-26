using ByteBait.Api.Dtos;
using ByteBait.Data.Entities;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ByteBait.Api.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController(UserManager<Player> players, SignInManager<Player> signIn) : ControllerBase
{
    // Creates the account and logs the player in
    [HttpPost("register")]
    public async Task<ActionResult<PlayerDto>> Register(RegisterDto dto)
    {
        var player = new Player { UserName = dto.UserName };

        IdentityResult result;
        try
        {
            result = await players.CreateAsync(player, dto.Password!);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two players took the same name at the same time: the unique index stopped the second
            return UserNameTaken(dto.UserName!);
        }

        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return UserNameTaken(dto.UserName!);
        }

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                // Identity's codes for a bad password all begin with Password, e.g. PasswordTooShort
                var field = error.Code.StartsWith("Password", StringComparison.Ordinal) ? "password" : "userName";

                // Identity's own text says only letters or digits, but we also allow æ, ø, å, - and _
                var message = error.Code == nameof(IdentityErrorDescriber.InvalidUserName)
                    ? "Username can only contain letters (also æ, ø and å), numbers, - and _."
                    : error.Description;
                ModelState.AddModelError(field, message);
            }

            return ValidationProblem(ModelState);
        }

        await signIn.SignInAsync(player, isPersistent: true);

        return CreatedAtAction(nameof(Me), PlayerDto.From(player, roles: []));
    }

    [HttpPost("login")]
    public async Task<ActionResult<PlayerDto>> Login(LoginDto dto)
    {
        var player = await players.FindByNameAsync(dto.UserName!);
        if (player is null)
        {
            return WrongLogin();
        }

        var result = await signIn.PasswordSignInAsync(player, dto.Password!, isPersistent: true, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too many failed attempts",
                detail: "The account is locked for a few minutes. Try again later.");
        }

        if (!result.Succeeded)
        {
            return WrongLogin();
        }

        return PlayerDto.From(player, await players.GetRolesAsync(player));
    }

    // Doesn't require a login, so logging out with an expired cookie isn't an error
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return NoContent();
    }

    // The token every POST must send in the X-XSRF-TOKEN header. Also sets the antiforgery cookie it is checked against.
    // The token belongs to the player, so the game gets a new one after register, login and logout.
    [HttpGet("antiforgery")]
    public ActionResult<AntiforgeryTokenDto> AntiforgeryToken([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return new AntiforgeryTokenDto(tokens.RequestToken!);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<PlayerDto>> Me()
    {
        // Null if the account was deleted while the player was logged in
        var player = await players.GetUserAsync(User);
        if (player is null)
        {
            return Unauthorized();
        }

        return PlayerDto.From(player, await players.GetRolesAsync(player));
    }

    private ObjectResult UserNameTaken(string userName) => Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Username is already taken",
        detail: $"The username '{userName}' is already in use.");

    // The same answer for an unknown name and a wrong password
    private ObjectResult WrongLogin() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Wrong username or password",
        detail: "The username or password is wrong.");
}
