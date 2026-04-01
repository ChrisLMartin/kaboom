using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Spendwise.Data;

public sealed class SpendwiseDbContextFactory : IDesignTimeDbContextFactory<SpendwiseDbContext>
{
    public SpendwiseDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Spendwise")
            ?? "Host=localhost;Port=5432;Database=spendwise;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<SpendwiseDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new SpendwiseDbContext(optionsBuilder.Options);
    }
}
