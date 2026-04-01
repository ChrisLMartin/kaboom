using Microsoft.EntityFrameworkCore;
using Spendwise.Data;
using Spendwise.Models;

namespace Spendwise.Services;

public sealed class PostgresBudgetRepository : IBudgetRepository
{
    private readonly IDbContextFactory<SpendwiseDbContext> _dbContextFactory;

    public PostgresBudgetRepository(IDbContextFactory<SpendwiseDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<BudgetData> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var createdUtc = await dbContext.BudgetStates
            .Select(item => item.CreatedUtc)
            .SingleOrDefaultAsync(cancellationToken);

        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var groups = await dbContext.CategoryGroups
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var categories = await dbContext.Categories
            .AsNoTracking()
            .OrderBy(item => item.GroupId)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .OrderBy(item => item.Date)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var monthlyBudgets = await dbContext.MonthlyBudgets
            .AsNoTracking()
            .OrderBy(item => item.MonthKey)
            .ToListAsync(cancellationToken);

        var allocations = await dbContext.CategoryAllocations
            .AsNoTracking()
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

    public async Task SaveAsync(BudgetData data, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.CategoryAllocations.ExecuteDeleteAsync(cancellationToken);
        await dbContext.MonthlyBudgets.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Transactions.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Categories.ExecuteDeleteAsync(cancellationToken);
        await dbContext.CategoryGroups.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Accounts.ExecuteDeleteAsync(cancellationToken);
        await dbContext.BudgetStates.ExecuteDeleteAsync(cancellationToken);

        dbContext.BudgetStates.Add(new BudgetStateEntity
        {
            Id = 1,
            CreatedUtc = EnsureUtc(data.CreatedUtc)
        });

        dbContext.Accounts.AddRange(data.Accounts.Select(item => new AccountEntity
        {
            Id = item.Id,
            Name = item.Name,
            Kind = item.Kind,
            Balance = item.Balance
        }));

        dbContext.CategoryGroups.AddRange(data.CategoryGroups.Select(group => new CategoryGroupEntity
        {
            Id = group.Id,
            Name = group.Name
        }));

        dbContext.Categories.AddRange(data.CategoryGroups
            .SelectMany(group => group.Categories.Select(category => new CategoryEntity
            {
                Id = category.Id,
                GroupId = group.Id,
                Name = category.Name,
                MonthlyTarget = category.MonthlyTarget
            })));

        dbContext.Transactions.AddRange(data.Transactions.Select(item => new TransactionEntity
        {
            Id = item.Id,
            Date = item.Date.Date,
            AccountId = item.AccountId,
            CategoryId = item.CategoryId,
            Payee = item.Payee,
            Notes = item.Notes,
            Amount = item.Amount
        }));

        dbContext.MonthlyBudgets.AddRange(data.MonthlyBudgets.Select(item => new MonthlyBudgetEntity
        {
            MonthKey = item.MonthKey
        }));

        dbContext.CategoryAllocations.AddRange(data.MonthlyBudgets
            .SelectMany(month => month.Allocations.Select(allocation => new CategoryAllocationEntity
            {
                MonthKey = month.MonthKey,
                CategoryId = allocation.CategoryId,
                AssignedAmount = allocation.AssignedAmount
            })));

        await dbContext.SaveChangesAsync(cancellationToken);
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
