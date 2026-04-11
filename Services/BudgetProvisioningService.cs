using Kaboom.Data;

namespace Kaboom.Services;

public sealed class BudgetProvisioningService
{
    private readonly KaboomDbContext _dbContext;
    private readonly BudgetSeedLoader _seedLoader;
    private readonly IBudgetRepository _repository;

    public BudgetProvisioningService(
        KaboomDbContext dbContext,
        BudgetSeedLoader seedLoader,
        IBudgetRepository repository)
    {
        _dbContext = dbContext;
        _seedLoader = seedLoader;
        _repository = repository;
    }

    public async Task<string> EnsureBudgetForUserAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        var existingBudgetId = _dbContext.BudgetMemberships
            .Where(item => item.UserId == user.Id)
            .OrderBy(item => item.CreatedUtc)
            .Select(item => item.BudgetId)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(existingBudgetId))
        {
            return existingBudgetId;
        }

        var budgetId = Guid.NewGuid().ToString("n");
        var budgetName = string.IsNullOrWhiteSpace(user.DisplayName)
            ? "My Budget"
            : $"{user.DisplayName}'s Budget";

        _dbContext.Budgets.Add(new BudgetEntity
        {
            Id = budgetId,
            Name = budgetName,
            CreatedUtc = DateTime.UtcNow
        });

        _dbContext.BudgetMemberships.Add(new BudgetMembershipEntity
        {
            BudgetId = budgetId,
            UserId = user.Id,
            Role = "Owner",
            CreatedUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var seed = await _seedLoader.LoadAsync(cancellationToken);
        await _repository.SaveAsync(budgetId, seed, cancellationToken);

        return budgetId;
    }
}
