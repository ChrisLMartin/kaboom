using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Kaboom.Data;

namespace Kaboom.Services;

public sealed record BudgetAccessContext(
    string BudgetId,
    string BudgetName,
    string UserId,
    string Email,
    string DisplayName);

public sealed class CurrentBudgetService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly KaboomDbContext _dbContext;
    private readonly BudgetProvisioningService _budgetProvisioningService;

    public CurrentBudgetService(
        IHttpContextAccessor httpContextAccessor,
        UserManager<ApplicationUser> userManager,
        KaboomDbContext dbContext,
        BudgetProvisioningService budgetProvisioningService)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _dbContext = dbContext;
        _budgetProvisioningService = budgetProvisioningService;
    }

    public async Task<BudgetAccessContext> GetRequiredAsync(CancellationToken cancellationToken = default)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException();
        }

        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
        {
            throw new UnauthorizedAccessException();
        }

        return await GetOrCreateForUserAsync(user, cancellationToken);
    }

    public async Task<BudgetAccessContext> GetOrCreateForUserAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        var context = await _dbContext.BudgetMemberships
            .AsNoTracking()
            .Where(item => item.UserId == user.Id)
            .OrderBy(item => item.CreatedUtc)
            .Join(
                _dbContext.Budgets.AsNoTracking(),
                membership => membership.BudgetId,
                budget => budget.Id,
                (membership, budget) => new BudgetAccessContext(
                    budget.Id,
                    budget.Name,
                    user.Id,
                    user.Email ?? string.Empty,
                    string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email ?? "Kaboom User" : user.DisplayName))
            .FirstOrDefaultAsync(cancellationToken);

        if (context is not null)
        {
            return context;
        }

        await _budgetProvisioningService.EnsureBudgetForUserAsync(user, cancellationToken);

        var createdContext = await _dbContext.BudgetMemberships
            .AsNoTracking()
            .Where(item => item.UserId == user.Id)
            .OrderBy(item => item.CreatedUtc)
            .Join(
                _dbContext.Budgets.AsNoTracking(),
                membership => membership.BudgetId,
                budget => budget.Id,
                (membership, budget) => new BudgetAccessContext(
                    budget.Id,
                    budget.Name,
                    user.Id,
                    user.Email ?? string.Empty,
                    string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email ?? "Kaboom User" : user.DisplayName))
            .FirstOrDefaultAsync(cancellationToken);

        return createdContext ?? throw new InvalidOperationException("Unable to provision a budget for the signed-in user.");
    }
}
