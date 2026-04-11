using Kaboom.Models;

namespace Kaboom.Services;

public sealed class BudgetCalculator
{
    public BudgetMonthViewModel BuildMonth(BudgetData data, string monthKey)
    {
        var monthDate = ParseMonthKey(monthKey);
        var assignedMap = GetAssignedMap(data, monthKey);
        var activityMap = GetActivityMap(data, monthKey);
        var availableMap = GetAvailableMap(data, monthKey);

        var rows = new List<CategoryBudgetRow>();
        foreach (var group in data.CategoryGroups.OrderBy(group => group.Name))
        {
            foreach (var category in group.Categories.OrderBy(category => category.Name))
            {
                rows.Add(new CategoryBudgetRow(
                    group.Id,
                    group.Name,
                    category.Id,
                    category.Name,
                    category.MonthlyTarget,
                    assignedMap.GetValueOrDefault(category.Id),
                    activityMap.GetValueOrDefault(category.Id),
                    availableMap.GetValueOrDefault(category.Id)));
            }
        }

        var totalCash = data.Accounts.Sum(account => account.Balance);
        var readyToAssign = totalCash - rows.Sum(row => row.Available);

        return new BudgetMonthViewModel(
            monthKey,
            monthDate,
            totalCash,
            readyToAssign,
            rows);
    }

    public ReportViewModel BuildReport(BudgetData data, string monthKey)
    {
        var month = BuildMonth(data, monthKey);
        var spendingByGroup = month.CategoryRows
            .GroupBy(row => row.GroupName)
            .Select(group => new GroupSpendingRow(
                group.Key,
                Math.Abs(group.Where(row => row.Activity < 0m).Sum(row => row.Activity)),
                group.Sum(row => row.Available)))
            .OrderByDescending(row => row.Spent)
            .ToList();

        var targets = month.CategoryRows
            .Where(row => row.Target > 0m)
            .Select(row => new TargetProgressRow(
                row.GroupName,
                row.CategoryName,
                row.Target,
                row.Available,
                row.Target == 0m ? 0m : Math.Clamp(row.Available / row.Target, 0m, 1m)))
            .OrderByDescending(row => row.ProgressRatio)
            .ToList();

        return new ReportViewModel(month, spendingByGroup, targets);
    }

    public IReadOnlyList<TransactionListItem> BuildTransactions(BudgetData data)
    {
        return data.Transactions
            .OrderByDescending(transaction => transaction.Date)
            .Select(transaction => BuildTransactionListItem(data, transaction))
            .ToList();
    }

    public void SetAssignedAmounts(BudgetData data, string monthKey, IReadOnlyDictionary<string, decimal> assignments)
    {
        var month = EnsureMonth(data, monthKey);
        var knownCategoryIds = data.CategoryGroups.SelectMany(group => group.Categories).Select(category => category.Id).ToHashSet();

        month.Allocations.RemoveAll(allocation => !knownCategoryIds.Contains(allocation.CategoryId));

        foreach (var entry in assignments)
        {
            var allocation = month.Allocations.FirstOrDefault(item => item.CategoryId == entry.Key);
            if (allocation is null)
            {
                month.Allocations.Add(new CategoryAllocation
                {
                    CategoryId = entry.Key,
                    AssignedAmount = entry.Value
                });
            }
            else
            {
                allocation.AssignedAmount = entry.Value;
            }
        }
    }

    public void MoveMoney(BudgetData data, string monthKey, string fromCategoryId, string toCategoryId, decimal amount)
    {
        if (amount <= 0m || fromCategoryId == toCategoryId)
        {
            return;
        }

        var month = EnsureMonth(data, monthKey);
        var from = EnsureAllocation(month, fromCategoryId);
        var to = EnsureAllocation(month, toCategoryId);

        from.AssignedAmount -= amount;
        to.AssignedAmount += amount;
    }

    public decimal AutoAssignTargets(BudgetData data, string monthKey)
    {
        var month = BuildMonth(data, monthKey);
        var remaining = month.ReadyToAssign;
        if (remaining <= 0m)
        {
            return 0m;
        }

        var updatedAssignments = month.CategoryRows.ToDictionary(row => row.CategoryId, row => row.Assigned);
        decimal assigned = 0m;

        foreach (var row in month.CategoryRows.OrderBy(row => row.GroupName).ThenBy(row => row.CategoryName))
        {
            if (remaining <= 0m)
            {
                break;
            }

            var needed = Math.Max(row.Target - row.Available, 0m);
            if (needed <= 0m)
            {
                continue;
            }

            var slice = Math.Min(needed, remaining);
            updatedAssignments[row.CategoryId] = row.Assigned + slice;
            remaining -= slice;
            assigned += slice;
        }

        SetAssignedAmounts(data, monthKey, updatedAssignments);
        return assigned;
    }

    public static string ToMonthKey(DateTime date) => $"{date:yyyy-MM}";

    public static DateTime ParseMonthKey(string monthKey)
    {
        if (DateTime.TryParseExact($"{monthKey}-01", "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var month))
        {
            return month;
        }

        return new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    }

    private TransactionListItem BuildTransactionListItem(BudgetData data, TransactionRecord transaction)
    {
        var accountName = data.Accounts.FirstOrDefault(account => account.Id == transaction.AccountId)?.Name ?? "Unknown account";
        var categoryName = data.CategoryGroups
            .SelectMany(group => group.Categories)
            .FirstOrDefault(category => category.Id == transaction.CategoryId)?.Name ?? "Ready to assign";

        return new TransactionListItem(
            transaction.Id,
            transaction.Date,
            transaction.AccountId,
            accountName,
            transaction.CategoryId,
            categoryName,
            transaction.Payee,
            transaction.Notes,
            transaction.Amount);
    }

    private static MonthlyBudget EnsureMonth(BudgetData data, string monthKey)
    {
        var month = data.MonthlyBudgets.FirstOrDefault(item => item.MonthKey == monthKey);
        if (month is not null)
        {
            return month;
        }

        month = new MonthlyBudget { MonthKey = monthKey };
        data.MonthlyBudgets.Add(month);
        return month;
    }

    private static CategoryAllocation EnsureAllocation(MonthlyBudget month, string categoryId)
    {
        var allocation = month.Allocations.FirstOrDefault(item => item.CategoryId == categoryId);
        if (allocation is not null)
        {
            return allocation;
        }

        allocation = new CategoryAllocation { CategoryId = categoryId, AssignedAmount = 0m };
        month.Allocations.Add(allocation);
        return allocation;
    }

    private static Dictionary<string, decimal> GetAssignedMap(BudgetData data, string monthKey)
    {
        return data.MonthlyBudgets
            .FirstOrDefault(month => month.MonthKey == monthKey)?
            .Allocations
            .GroupBy(allocation => allocation.CategoryId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.AssignedAmount))
            ?? [];
    }

    private static Dictionary<string, decimal> GetActivityMap(BudgetData data, string monthKey)
    {
        var monthStart = ParseMonthKey(monthKey);
        var monthEnd = monthStart.AddMonths(1);

        return data.Transactions
            .Where(transaction => transaction.CategoryId is not null)
            .Where(transaction => transaction.Date >= monthStart && transaction.Date < monthEnd)
            .GroupBy(transaction => transaction.CategoryId!)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));
    }

    private static Dictionary<string, decimal> GetAvailableMap(BudgetData data, string monthKey)
    {
        var categoryIds = data.CategoryGroups.SelectMany(group => group.Categories).Select(category => category.Id).Distinct().ToList();
        var availability = categoryIds.ToDictionary(categoryId => categoryId, _ => 0m);

        var targetMonth = ParseMonthKey(monthKey);
        var firstMonth = GetFirstMonth(data, targetMonth);

        for (var cursor = firstMonth; cursor <= targetMonth; cursor = cursor.AddMonths(1))
        {
            var key = ToMonthKey(cursor);
            var assigned = GetAssignedMap(data, key);
            var activity = GetActivityMap(data, key);

            foreach (var categoryId in categoryIds)
            {
                availability[categoryId] += assigned.GetValueOrDefault(categoryId) + activity.GetValueOrDefault(categoryId);
            }
        }

        return availability;
    }

    private static DateTime GetFirstMonth(BudgetData data, DateTime fallback)
    {
        var budgetMonths = data.MonthlyBudgets
            .Select(month => ParseMonthKey(month.MonthKey));

        var transactionMonths = data.Transactions
            .Select(transaction => new DateTime(transaction.Date.Year, transaction.Date.Month, 1));

        return budgetMonths.Concat(transactionMonths).DefaultIfEmpty(fallback).Min();
    }
}

public sealed record BudgetMonthViewModel(
    string MonthKey,
    DateTime MonthDate,
    decimal TotalCash,
    decimal ReadyToAssign,
    IReadOnlyList<CategoryBudgetRow> CategoryRows);

public sealed record CategoryBudgetRow(
    string GroupId,
    string GroupName,
    string CategoryId,
    string CategoryName,
    decimal Target,
    decimal Assigned,
    decimal Activity,
    decimal Available);

public sealed record TransactionListItem(
    string Id,
    DateTime Date,
    string AccountId,
    string AccountName,
    string? CategoryId,
    string CategoryName,
    string Payee,
    string Notes,
    decimal Amount);

public sealed record GroupSpendingRow(
    string GroupName,
    decimal Spent,
    decimal Remaining);

public sealed record TargetProgressRow(
    string GroupName,
    string CategoryName,
    decimal Target,
    decimal Available,
    decimal ProgressRatio);

public sealed record ReportViewModel(
    BudgetMonthViewModel Month,
    IReadOnlyList<GroupSpendingRow> SpendingByGroup,
    IReadOnlyList<TargetProgressRow> Targets);
