using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ByteBait.Data.Tests;

public class DatabaseMigratorTests
{
    [Fact]
    public void Migrator_is_not_registered_by_default()
    {
        var services = AddData(new Dictionary<string, string?>());

        Assert.DoesNotContain(services, s => s.ImplementationType == typeof(DatabaseMigrator));
    }

    [Fact]
    public void Migrator_is_registered_when_MigrateOnStartup_is_true()
    {
        var services = AddData(new Dictionary<string, string?> { ["Database:MigrateOnStartup"] = "true" });

        Assert.Contains(services, s =>
            s.ServiceType == typeof(IHostedService) && s.ImplementationType == typeof(DatabaseMigrator));
    }

    // Only registers services, so no database is needed
    private static ServiceCollection AddData(Dictionary<string, string?> settings)
    {
        settings["ConnectionStrings:Default"] = "Host=localhost;Database=unused";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddByteBaitData(configuration);
        return services;
    }
}
