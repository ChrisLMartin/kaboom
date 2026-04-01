using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Spendwise.Data;
using Spendwise.Models;

namespace Spendwise.Services;

public sealed class DatabaseBootstrapper
{
    private readonly IDbContextFactory<SpendwiseDbContext> _dbContextFactory;
    private readonly PostgresBudgetRepository _repository;
    private readonly IWebHostEnvironment _environment;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public DatabaseBootstrapper(
        IDbContextFactory<SpendwiseDbContext> dbContextFactory,
        PostgresBudgetRepository repository,
        IWebHostEnvironment environment)
    {
        _dbContextFactory = dbContextFactory;
        _repository = repository;
        _environment = environment;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var hasData = await dbContext.Accounts.AnyAsync(cancellationToken)
            || await dbContext.CategoryGroups.AnyAsync(cancellationToken)
            || await dbContext.Transactions.AnyAsync(cancellationToken)
            || await dbContext.MonthlyBudgets.AnyAsync(cancellationToken);

        if (hasData)
        {
            return;
        }

        var seed = await LoadSeedDataAsync(cancellationToken);
        await _repository.SaveAsync(seed, cancellationToken);
    }

    private async Task<BudgetData> LoadSeedDataAsync(CancellationToken cancellationToken)
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
