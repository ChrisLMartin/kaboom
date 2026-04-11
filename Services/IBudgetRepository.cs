using Kaboom.Models;

namespace Kaboom.Services;

public interface IBudgetRepository
{
    Task<BudgetData> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(BudgetData data, CancellationToken cancellationToken = default);
}
