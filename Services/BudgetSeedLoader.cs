using System.Text.Json;
using System.Text.Json.Serialization;
using Kaboom.Models;

namespace Kaboom.Services;

public sealed class BudgetSeedLoader
{
    private readonly IWebHostEnvironment _environment;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public BudgetSeedLoader(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<BudgetData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var dataPath = Path.Combine(_environment.ContentRootPath, "App_Data", "budget-data.json");
        if (!File.Exists(dataPath))
        {
            return DemoDataFactory.Create();
        }

        await using var stream = File.OpenRead(dataPath);
        var data = await JsonSerializer.DeserializeAsync<BudgetData>(stream, _jsonOptions, cancellationToken);
        return data ?? DemoDataFactory.Create();
    }
}
