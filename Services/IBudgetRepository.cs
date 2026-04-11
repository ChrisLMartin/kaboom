using Kaboom.Models;

namespace Kaboom.Services;

public interface IBudgetRepository
{
    Task<BudgetData> GetAsync(string budgetId, CancellationToken cancellationToken = default);
    Task SaveAsync(string budgetId, BudgetData data, CancellationToken cancellationToken = default);
}
