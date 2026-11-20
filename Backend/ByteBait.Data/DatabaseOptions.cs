namespace ByteBait.Data;

// The "Database" section of the configuration. In Dokploy: Database__MigrateOnStartup=true
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    // Off by default, so tests and local runs never change a database by themselves
    public bool MigrateOnStartup { get; init; }
}
