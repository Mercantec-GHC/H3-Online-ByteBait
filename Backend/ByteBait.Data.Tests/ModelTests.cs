using Microsoft.EntityFrameworkCore;

namespace ByteBait.Data.Tests;

public class ModelTests
{
    // Fails if an entity was changed without adding a migration. Doesn't need a database.
    [Fact]
    public void Model_has_no_changes_missing_a_migration()
    {
        var options = new DbContextOptionsBuilder<ByteBaitDbContext>();
        DataServiceCollectionExtensions.ConfigureDbContext(options, "Host=localhost;Database=unused");
        using var db = new ByteBaitDbContext(options.Options);

        Assert.False(db.Database.HasPendingModelChanges(),
            "Model changed without a migration. Run: dotnet ef migrations add <Name> -p Backend/ByteBait.Data");
    }
}
