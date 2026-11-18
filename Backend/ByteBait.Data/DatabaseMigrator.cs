using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ByteBait.Data;

// Brings the database up to date when the API starts, before it begins taking requests.
// Used on Test-ByteBait and Main-ByteBait, so a deploy also updates the tables on that environment's Neon branch.
// Each environment runs a single container, so two instances never migrate the same database at once.
internal sealed class DatabaseMigrator(IServiceScopeFactory scopeFactory, ILogger<DatabaseMigrator> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // The DbContext is scoped, and a hosted service is a singleton, so it gets its own scope
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ByteBaitDbContext>();

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Database is up to date");
            return;
        }

        logger.LogInformation("Applying migrations: {Migrations}", string.Join(", ", pending));
        await db.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
