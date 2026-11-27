using ByteBait.Data;
using ByteBait.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace ByteBait.Api.Players;

public static class PlayerAccountExtensions
{
    public const string LoginCookieName = "bytebait_login";

    public const string AntiforgeryCookieName = "bytebait_antiforgery";

    // The game sends the token from GET /api/account/antiforgery in this header on every POST
    public const string AntiforgeryHeaderName = "X-XSRF-TOKEN";

    // Player accounts with ASP.NET Core Identity and a login cookie (S2.13)
    public static IServiceCollection AddPlayerAccounts(this IServiceCollection services)
    {
        // The keys that protect the cookie are kept in the database, so a deploy doesn't log everyone out
        services.AddDataProtection()
            .SetApplicationName("ByteBait")
            .PersistKeysToDbContext<ByteBaitDbContext>();

        services.AddIdentityCore<Player>(options =>
            {
                options.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzæøåABCDEFGHIJKLMNOPQRSTUVWXYZÆØÅ0123456789-_";

                // A long password is better than a short one with odd characters, so only the length counts
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                // Stops someone from guessing a password by trying again and again
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ByteBaitDbContext>()
            .AddSignInManager();

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddApplicationCookie();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = LoginCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            // The game and the API are on the same domain, so other sites never need to send the cookie
            options.Cookie.SameSite = SameSiteMode.Strict;
            // Needed to log in, so it doesn't wait for the cookie banner (S2.15)
            options.Cookie.IsEssential = true;

            // Seven days since the player was last active
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;

            // The cookie handler points to a login page by default. An API just answers with the status.
            options.Events.OnRedirectToLogin = context => SetStatus(context, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => SetStatus(context, StatusCodes.Status403Forbidden);
        });

        // Protection against CSRF on every POST, PUT, PATCH and DELETE to the controllers (see Program.cs).
        // Only https, like the login cookie, so locally the API must run with the https profile.
        services.AddAntiforgery(options =>
        {
            options.HeaderName = AntiforgeryHeaderName;
            options.Cookie.Name = AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            // Needed to log in and save casts, so it doesn't wait for the cookie banner (S2.15)
            options.Cookie.IsEssential = true;
        });

        // The cookie policy (S2.15, app.UseCookiePolicy in Program.cs). All our cookies are strictly necessary,
        // so no consent is needed. The rules are a safety net: a cookie added later is still HttpOnly, Secure and Strict.
        // A tracking or statistics cookie would need active consent first (see /cookies).
        services.Configure<CookiePolicyOptions>(options =>
        {
            options.CheckConsentNeeded = _ => false;
            options.MinimumSameSitePolicy = SameSiteMode.Strict;
            options.Secure = CookieSecurePolicy.Always;
            options.HttpOnly = HttpOnlyPolicy.Always;
        });

        // CurrentPlayer reads the logged-in player from the request
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentPlayer>();

        return services;
    }

    private static Task SetStatus(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
