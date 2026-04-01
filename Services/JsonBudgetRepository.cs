using System.Text.Json;
using System.Text.Json.Serialization;
using Spendwise.Models;

namespace Spendwise.Services;

public sealed class JsonBudgetRepository : IBudgetRepository
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _dataPath;

    public JsonBudgetRepository(IWebHostEnvironment environment)
    {
        var dataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);
        _dataPath = Path.Combine(dataDirectory, "budget-data.json");
    }

    public async Task<BudgetData> GetAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_dataPath))
            {
                var seed = DemoDataFactory.Create();
                await SaveInternalAsync(seed, cancellationToken);
                return seed;
            }

            await using var stream = File.OpenRead(_dataPath);
            var data = await JsonSerializer.DeserializeAsync<BudgetData>(stream, _jsonOptions, cancellationToken);
            return data ?? DemoDataFactory.Create();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(BudgetData data, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await SaveInternalAsync(data, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveInternalAsync(BudgetData data, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_dataPath);
        await JsonSerializer.SerializeAsync(stream, data, _jsonOptions, cancellationToken);
    }
}
