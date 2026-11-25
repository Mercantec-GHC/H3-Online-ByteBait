namespace ByteBait.Api.Tests;

// Skips the test unless ConnectionStrings__Default is set (CI sets it to its Postgres container).
// Same idea as PostgresFactAttribute in ByteBait.Data.Tests.
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (!ApiFactory.HasDatabase)
        {
            Skip = "ConnectionStrings__Default is not set";
        }
    }
}

// The same for a test with several cases
public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (!ApiFactory.HasDatabase)
        {
            Skip = "ConnectionStrings__Default is not set";
        }
    }
}
