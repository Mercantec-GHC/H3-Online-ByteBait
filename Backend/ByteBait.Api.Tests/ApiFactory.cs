using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ByteBait.Api.Tests;

// Starts the API in memory for the tests.
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // Set by CI to its Postgres container. User-secrets are never read here, so the tests can't write to Neon.
    public static string? ConnectionString => Environment.GetEnvironmentVariable("ConnectionStrings__Default");

    public static bool HasDatabase => !string.IsNullOrWhiteSpace(ConnectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development", so user-secrets are not loaded
        builder.UseEnvironment("Testing");

        // Blazor files from Frontend/ByteBait.Client are only found automatically in Development
        builder.UseStaticWebAssets();

        // Validation tests never reach the database, so they can run without one
        builder.UseSetting("ConnectionStrings:Default", HasDatabase ? ConnectionString : "Host=localhost;Database=unused");
    }
}
