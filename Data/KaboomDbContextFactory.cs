using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kaboom.Data;

public sealed class KaboomDbContextFactory : IDesignTimeDbContextFactory<KaboomDbContext>
{
    public KaboomDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Kaboom")
            ?? "Host=localhost;Port=5432;Database=kaboom;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<KaboomDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new KaboomDbContext(optionsBuilder.Options);
    }
}
