using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ByteBait.Data;

// Only used by the dotnet ef tool, so migrations can run from this project without starting the API
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ByteBaitDbContext>
{
    public ByteBaitDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<DesignTimeDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString(DataServiceCollectionExtensions.ConnectionStringName)
            ?? throw new InvalidOperationException(
                "Missing connection string 'Default'. See Backend/README.md for how to set it with dotnet user-secrets.");

        var options = new DbContextOptionsBuilder<ByteBaitDbContext>();
        DataServiceCollectionExtensions.ConfigureDbContext(options, connectionString);
        return new ByteBaitDbContext(options.Options);
    }
}
