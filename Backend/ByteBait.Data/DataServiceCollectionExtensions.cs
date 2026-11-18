using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ByteBait.Data;

public static class DataServiceCollectionExtensions
{
    public const string ConnectionStringName = "Default";

    // Call from Program.cs in the API: builder.Services.AddByteBaitData(builder.Configuration);
    // Locally the connection string comes from user-secrets, in CI and Dokploy from ConnectionStrings__Default.
    public static IServiceCollection AddByteBaitData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                "Missing connection string 'Default'. Set it with dotnet user-secrets or ConnectionStrings__Default.");

        services.AddDbContext<ByteBaitDbContext>(options => ConfigureDbContext(options, connectionString));

        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
            ?? new DatabaseOptions();

        if (databaseOptions.MigrateOnStartup)
        {
            services.AddHostedService<DatabaseMigrator>();
        }

        return services;
    }

    // Also used by the design-time factory and the tests, so everything is set up the same way
    public static void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString)
    {
        options
            .UseNpgsql(connectionString, npgsql =>
                // Neon suspends the database when idle, and the first connection afterwards can fail
                npgsql.EnableRetryOnFailure(maxRetryCount: 3))
            .UseSnakeCaseNamingConvention();
    }
}
