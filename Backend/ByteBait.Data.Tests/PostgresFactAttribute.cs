namespace ByteBait.Data.Tests;

// Skips the test unless ConnectionStrings__Default is set (CI sets it to its Postgres container).
// User-secrets are not read here, so the tests never run against Neon by accident.
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(PostgresDatabase.ConnectionString))
        {
            Skip = "ConnectionStrings__Default is not set";
        }
    }
}
