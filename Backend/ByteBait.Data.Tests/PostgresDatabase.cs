using Microsoft.EntityFrameworkCore;

namespace ByteBait.Data.Tests;

// Shared by the database tests. Runs the migrations once before the tests start.
public sealed class PostgresDatabase
{
    public static string? ConnectionString => Environment.GetEnvironmentVariable("ConnectionStrings__Default");

    public PostgresDatabase()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) return;
        using var db = CreateContext();
        db.Database.Migrate();
    }

    public ByteBaitDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ByteBaitDbContext>();
        DataServiceCollectionExtensions.ConfigureDbContext(options, ConnectionString!);
        return new ByteBaitDbContext(options.Options);
    }
}
