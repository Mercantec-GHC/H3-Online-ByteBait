namespace ByteBait.Data.Tests;

// All database test classes share one PostgresDatabase, so the migrations run once.
// With a fixture per class, xUnit runs the classes in parallel and two of them migrate at the same time.
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresDatabase>
{
    public const string Name = "Postgres";
}
