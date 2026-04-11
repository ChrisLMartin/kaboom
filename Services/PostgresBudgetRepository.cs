using Microsoft.EntityFrameworkCore;
using Kaboom.Data;
using Kaboom.Models;

namespace Kaboom.Services;

public sealed class PostgresBudgetRepository : IBudgetRepository
{
    private readonly KaboomDbContext _dbContext;

    public PostgresBudgetRepository(KaboomDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BudgetData> GetAsync(string budgetId, CancellationToken cancellationToken = default)
    {
        var createdUtc = await _dbContext.BudgetStates
            .Where(item => item.BudgetId == budgetId)
            .Select(item => item.CreatedUtc)
            .SingleOrDefaultAsync(cancellationToken);

        var accounts = await _dbContext.Accounts
            .AsNoTracking()
            .Where(item => item.BudgetId == budgetId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var groups = await _dbContext.CategoryGroups
            .AsNoTracking()
            .Where(item => item.BudgetId == budgetId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var categories = await _dbContext.Categories
            .AsNoTracking()
            .Where(item => item.BudgetId == budgetId)
            .OrderBy(item => item.GroupId)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var transactions = await _dbContext.Transactions
            .AsNoTracking()
            .Where(item => item.BudgetId == budgetId)
            .OrderBy(item => item.Date)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var monthlyBudgets = await _dbContext.MonthlyBudgets
            .AsNoTracking()
            .Where(item => item.BudgetId == budgetId)
            .OrderBy(item => item.MonthKey)
            .ToListAsync(cancellationToken);

        var allocations = await _dbContext.CategoryAllocations
            .AsNoTracking()
            .Where(item => item.BudgetId == budgetId)
            .OrderBy(item => item.MonthKey)
            .ThenBy(item => item.CategoryId)
            .ToListAsync(cancellationToken);

        return new BudgetData
        {
            CreatedUtc = createdUtc == default ? DateTime.UtcNow : EnsureUtc(createdUtc),
            Accounts = accounts
                .Select(item => new Account
                {
                    Id = item.Id,
                    Name = item.Name,
                    Kind = item.Kind,
                    Balance = item.Balance
                })
                .ToList(),
            CategoryGroups = groups
                .Select(group => new CategoryGroup
                {
                    Id = group.Id,
                    Name = group.Name,
                    Categories = categories
                        .Where(category => category.GroupId == group.Id)
                        .Select(category => new BudgetCategory
                        {
                            Id = category.Id,
                            Name = category.Name,
                            MonthlyTarget = category.MonthlyTarget
                        })
                        .ToList()
                })
                .ToList(),
            Transactions = transactions
                .Select(item => new TransactionRecord
                {
                    Id = item.Id,
                    Date = item.Date.Date,
                    AccountId = item.AccountId,
                    CategoryId = item.CategoryId,
                    Payee = item.Payee,
                    Notes = item.Notes,
                    Amount = item.Amount
                })
                .ToList(),
            MonthlyBudgets = monthlyBudgets
                .Select(month => new MonthlyBudget
                {
                    MonthKey = month.MonthKey,
                    Allocations = allocations
                        .Where(allocation => allocation.MonthKey == month.MonthKey)
                        .Select(allocation => new CategoryAllocation
                        {
                            CategoryId = allocation.CategoryId,
                            AssignedAmount = allocation.AssignedAmount
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    public async Task SaveAsync(string budgetId, BudgetData data, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.CategoryAllocations
            .Where(item => item.BudgetId == budgetId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.MonthlyBudgets
            .Where(item => item.BudgetId == budgetId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Transactions
            .Where(item => item.BudgetId == budgetId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Categories
            .Where(item => item.BudgetId == budgetId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.CategoryGroups
            .Where(item => item.BudgetId == budgetId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Accounts
            .Where(item => item.BudgetId == budgetId)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.BudgetStates
            .Where(item => item.BudgetId == budgetId)
            .ExecuteDeleteAsync(cancellationToken);

        _dbContext.BudgetStates.Add(new BudgetStateEntity
        {
            BudgetId = budgetId,
            CreatedUtc = EnsureUtc(data.CreatedUtc)
        });

        _dbContext.Accounts.AddRange(data.Accounts.Select(item => new AccountEntity
        {
            Id = item.Id,
            BudgetId = budgetId,
            Name = item.Name,
            Kind = item.Kind,
            Balance = item.Balance
        }));

        _dbContext.CategoryGroups.AddRange(data.CategoryGroups.Select(group => new CategoryGroupEntity
        {
            Id = group.Id,
            BudgetId = budgetId,
            Name = group.Name
        }));

        _dbContext.Categories.AddRange(data.CategoryGroups
            .SelectMany(group => group.Categories.Select(category => new CategoryEntity
            {
                Id = category.Id,
                BudgetId = budgetId,
                GroupId = group.Id,
                Name = category.Name,
                MonthlyTarget = category.MonthlyTarget
            })));

        _dbContext.Transactions.AddRange(data.Transactions.Select(item => new TransactionEntity
        {
            Id = item.Id,
            BudgetId = budgetId,
            Date = item.Date.Date,
            AccountId = item.AccountId,
            CategoryId = item.CategoryId,
            Payee = item.Payee,
            Notes = item.Notes,
            Amount = item.Amount
        }));

        _dbContext.MonthlyBudgets.AddRange(data.MonthlyBudgets.Select(item => new MonthlyBudgetEntity
        {
            BudgetId = budgetId,
            MonthKey = item.MonthKey
        }));

        _dbContext.CategoryAllocations.AddRange(data.MonthlyBudgets
            .SelectMany(month => month.Allocations.Select(allocation => new CategoryAllocationEntity
            {
                BudgetId = budgetId,
                MonthKey = month.MonthKey,
                CategoryId = allocation.CategoryId,
                AssignedAmount = allocation.AssignedAmount
            })));

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
