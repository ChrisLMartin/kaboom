using System.Globalization;
using Spendwise.Api;
using Spendwise.Models;

namespace Spendwise.Services;

public sealed class SpendwiseApiService
{
    private readonly IBudgetRepository _repository;
    private readonly BudgetCalculator _calculator;

    public SpendwiseApiService(IBudgetRepository repository, BudgetCalculator calculator)
    {
        _repository = repository;
        _calculator = calculator;
    }

    public async Task<IReadOnlyList<AccountDto>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetAsync(cancellationToken);
        return BuildAccounts(data);
    }

    public async Task<AccountDto> AddAccountAsync(AccountUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetAsync(cancellationToken);
        var account = new Account
        {
            Name = RequireValue(request.Name, "Account name is required."),
            Kind = ParseAccountKind(request.Kind),
            Balance = request.Balance
        };

        data.Accounts.Add(account);
        await _repository.SaveAsync(data, cancellationToken);
        return BuildAccount(account);
    }

    public async Task<AccountDto> UpdateAccountAsync(string accountId, AccountUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetAsync(cancellationToken);
        var account = data.Accounts.FirstOrDefault(item => item.Id == accountId)
            ?? throw new KeyNotFoundException("Account not found.");

        account.Name = RequireValue(request.Name, "Account name is required.");
        account.Kind = ParseAccountKind(request.Kind);
        account.Balance = request.Balance;

        await _repository.SaveAsync(data, cancellationToken);
        return BuildAccount(account);
    }

    public async Task<IReadOnlyList<CategoryGroupDto>> GetCategoryGroupsAsync(CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetAsync(cancellationToken);
        return BuildCategoryGroups(data);
    }

    public async Task<CategoryGroupDto> RenameCategoryGroupAsync(string groupId, RenameEntityRequest request, CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetAsync(cancellationToken);
        var group = data.CategoryGroups.FirstOrDefault(item => item.Id == groupId)
            ?? throw new KeyNotFoundException("Category group not found.");

        group.Name = RequireValue(request.Name, "Group name is required.");
        await _repository.SaveAsync(data, cancellationToken);
        return BuildCategoryGroup(group);
    }

    public async Task<CategoryDto> RenameCategoryAsync(string categoryId, RenameEntityRequest request, CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetAsync(cancellationToken);
        var category = data.CategoryGroups
            .SelectMany(group => group.Categories)
            .FirstOrDefault(item => item.Id == categoryId)
            ?? throw new KeyNotFoundException("Category not found.");

        category.Name = RequireValue(request.Name, "Category name is required.");
        await _repository.SaveAsync(data, cancellationToken);
        return BuildCategory(category);
    }

    public async Task<CategoryDto> AddCategoryAsync(AddCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetAsync(cancellationToken);
        var categoryName = RequireValue(request.Name, "Category name is required.");

        CategoryGroup? group = null;
        if (!string.IsNullOrWhiteSpace(request.NewGroupName))
        {
            group = new CategoryGroup
            {
                Name = RequireValue(request.NewGroupName, "New group name is required.")
            };

            data.CategoryGroups.Add(group);
        }
        else if (!string.IsNullOrWhiteSpace(request.GroupId))
        {
            group = data.CategoryGroups.FirstOrDefault(item => item.Id == request.GroupId);
        }

        if (group is null)
        {
            throw new ArgumentException("Choose an existing group or create a new one.");
        }

        var category = new BudgetCategory
        {
            Name = categoryName
        };

        group.Categories.Add(category);
        await _repository.SaveAsync(data, cancellationToken);
        return BuildCategory(category);
    }

    public async Task<BudgetPageDto> GetBudgetPageAsync(string? startMonthKey, int monthCount = 3, CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetAsync(cancellationToken);
        return BuildBudgetPage(data, NormalizeMonthKey(startMonthKey), monthCount);
    }

    public async Task<BudgetPageDto> SaveBudgetAsync(SaveBudgetRequest request, CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetAsync(cancellationToken);
        var invalidUpdates = new List<string>();

        foreach (var monthUpdates in request.Updates
                     .GroupBy(update => NormalizeMonthKey(update.MonthKey)))
        {
            var assignments = data.MonthlyBudgets
                .FirstOrDefault(item => item.MonthKey == monthUpdates.Key)?
                .Allocations
                .ToDictionary(item => item.CategoryId, item => item.AssignedAmount)
                ?? new Dictionary<string, decimal>();

            foreach (var update in monthUpdates)
            {
                if (!TryParseBudgetAmount(update.Value, out var parsed))
                {
                    invalidUpdates.Add($"{update.MonthKey}:{update.CategoryId}");
                    continue;
                }

                assignments[update.CategoryId] = parsed;
            }

            if (invalidUpdates.Count == 0)
            {
                _calculator.SetAssignedAmounts(data, monthUpdates.Key, assignments);
            }
        }

        if (invalidUpdates.Count > 0)
        {
            throw new ArgumentException("Finish the budget math before saving.");
        }

        await _repository.SaveAsync(data, cancellationToken);
        return BuildBudgetPage(data, NormalizeMonthKey(request.StartMonthKey), request.MonthCount);
    }

    public async Task<TransactionsPageDto> GetTransactionsPageAsync(CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetAsync(cancellationToken);
        return BuildTransactionsPage(data);
    }

    public async Task<TransactionsPageDto> AddTransactionAsync(TransactionUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetAsync(cancellationToken);
        var account = data.Accounts.FirstOrDefault(item => item.Id == request.AccountId)
            ?? throw new ArgumentException("Choose an account.");
        var payee = RequireValue(request.Payee, "Payee is required.");

        data.Transactions.Add(new TransactionRecord
        {
            Date = request.Date == default ? DateTime.Today : request.Date.Date,
            AccountId = account.Id,
            CategoryId = string.IsNullOrWhiteSpace(request.CategoryId) ? null : request.CategoryId,
            Payee = payee,
            Notes = request.Notes?.Trim() ?? string.Empty,
            Amount = request.Amount
        });

        account.Balance += request.Amount;
        await _repository.SaveAsync(data, cancellationToken);
        return BuildTransactionsPage(data);
    }

    public async Task<TransactionsPageDto> UpdateTransactionAsync(string transactionId, TransactionUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetAsync(cancellationToken);
        var transaction = data.Transactions.FirstOrDefault(item => item.Id == transactionId)
            ?? throw new KeyNotFoundException("Transaction not found.");
        var nextAccount = data.Accounts.FirstOrDefault(item => item.Id == request.AccountId)
            ?? throw new ArgumentException("Choose an account.");
        var payee = RequireValue(request.Payee, "Payee is required.");

        var previousAccount = data.Accounts.FirstOrDefault(item => item.Id == transaction.AccountId);
        if (previousAccount is not null)
        {
            previousAccount.Balance -= transaction.Amount;
        }

        nextAccount.Balance += request.Amount;
        transaction.Date = request.Date == default ? DateTime.Today : request.Date.Date;
        transaction.AccountId = nextAccount.Id;
        transaction.CategoryId = string.IsNullOrWhiteSpace(request.CategoryId) ? null : request.CategoryId;
        transaction.Payee = payee;
        transaction.Notes = request.Notes?.Trim() ?? string.Empty;
        transaction.Amount = request.Amount;

        await _repository.SaveAsync(data, cancellationToken);
        return BuildTransactionsPage(data);
    }

    public async Task<ReportsPageDto> GetReportsAsync(string? monthKey, CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetAsync(cancellationToken);
        var normalizedMonthKey = NormalizeMonthKey(monthKey);
        var report = _calculator.BuildReport(data, normalizedMonthKey);

        return new ReportsPageDto(
            report.Month.MonthKey,
            report.Month.MonthDate.ToString("MMMM yyyy"),
            report.SpendingByGroup.Select(item => new ReportSpendingRowDto(
                item.GroupName,
                item.Spent,
                item.Spent.ToString("C"),
                item.Remaining,
                item.Remaining.ToString("C"))).ToList(),
            report.Targets.Select(item => new ReportTargetRowDto(
                item.GroupName,
                item.CategoryName,
                item.Target,
                item.Target.ToString("C"),
                item.Available,
                item.Available.ToString("C"),
                item.ProgressRatio)).ToList());
    }

    private BudgetPageDto BuildBudgetPage(BudgetData data, string startMonthKey, int requestedMonthCount)
    {
        var monthCount = Math.Clamp(requestedMonthCount, 1, 6);
        var startMonth = BudgetCalculator.ParseMonthKey(startMonthKey);
        var visibleMonths = Enumerable.Range(0, monthCount)
            .Select(offset => _calculator.BuildMonth(data, BudgetCalculator.ToMonthKey(startMonth.AddMonths(offset))))
            .ToList();

        var summaries = visibleMonths
            .Select(month =>
            {
                var previousMonth = _calculator.BuildMonth(data, BudgetCalculator.ToMonthKey(month.MonthDate.AddMonths(-1)));
                var previousOverspent = previousMonth.CategoryRows
                    .Where(row => row.Available < 0m)
                    .Sum(row => row.Available);
                var currentIncome = GetIncomeForMonth(data, month.MonthDate);
                var currentBudgetedEffect = -month.CategoryRows.Sum(row => row.Assigned);

                return new BudgetMonthSummaryDto(
                    month.MonthKey,
                    month.MonthDate.ToString("MMMM yyyy"),
                    month.TotalCash,
                    month.ReadyToAssign,
                    month.ReadyToAssign.ToString("C"),
                    new[]
                    {
                        BuildBreakdown($"Not budgeted in {previousMonth.MonthDate:MMM}", previousMonth.ReadyToAssign),
                        BuildBreakdown($"Overspent in {previousMonth.MonthDate:MMM}", previousOverspent),
                        BuildBreakdown($"Income for {month.MonthDate:MMM}", currentIncome),
                        BuildBreakdown($"Budgeted for {month.MonthDate:MMM}", currentBudgetedEffect)
                    },
                    month.CategoryRows.Sum(row => row.Assigned),
                    month.CategoryRows.Sum(row => row.Assigned).ToString("C"),
                    Math.Abs(month.CategoryRows.Where(row => row.Activity < 0m).Sum(row => row.Activity)),
                    Math.Abs(month.CategoryRows.Where(row => row.Activity < 0m).Sum(row => row.Activity)).ToString("C"),
                    month.CategoryRows.Sum(row => row.Available),
                    month.CategoryRows.Sum(row => row.Available).ToString("C"));
            })
            .ToList();

        var groups = data.CategoryGroups
            .OrderBy(group => group.Name)
            .Select(group => BuildBudgetGroup(group, visibleMonths))
            .ToList();

        return new BudgetPageDto(startMonthKey, summaries, groups);
    }

    private static BudgetGroupRowDto BuildBudgetGroup(CategoryGroup group, IReadOnlyList<BudgetMonthViewModel> visibleMonths)
    {
        var categories = group.Categories
            .OrderBy(category => category.Name)
            .Select(category => new BudgetCategoryRowDto(
                category.Id,
                category.Name,
                visibleMonths.Select(month =>
                {
                    var row = month.CategoryRows.First(item => item.CategoryId == category.Id);
                    return BuildBudgetCell(month.MonthKey, row.Assigned, row.Activity, row.Available);
                }).ToList()))
            .ToList();

        var totals = visibleMonths
            .Select(month =>
            {
                var rows = month.CategoryRows.Where(row => row.GroupId == group.Id).ToList();
                return BuildBudgetCell(
                    month.MonthKey,
                    rows.Sum(row => row.Assigned),
                    rows.Sum(row => row.Activity),
                    rows.Sum(row => row.Available));
            })
            .ToList();

        return new BudgetGroupRowDto(group.Id, group.Name, totals, categories);
    }

    private static BudgetCellDto BuildBudgetCell(string monthKey, decimal budgeted, decimal outflows, decimal balance)
    {
        return new BudgetCellDto(
            monthKey,
            budgeted,
            budgeted.ToString("0.00", CultureInfo.InvariantCulture),
            budgeted.ToString("C"),
            outflows,
            outflows.ToString("C"),
            balance,
            balance.ToString("C"));
    }

    private static BudgetMonthBreakdownDto BuildBreakdown(string label, decimal amount)
    {
        var effect = amount switch
        {
            > 0m => "positive",
            < 0m => "negative",
            _ => "neutral"
        };

        return new BudgetMonthBreakdownDto(
            label,
            amount,
            FormatSignedCurrency(amount),
            effect);
    }

    private TransactionsPageDto BuildTransactionsPage(BudgetData data)
    {
        return new TransactionsPageDto(
            BuildAccounts(data),
            BuildCategoryGroups(data),
            _calculator.BuildTransactions(data).Select(BuildTransaction).ToList());
    }

    private static IReadOnlyList<AccountDto> BuildAccounts(BudgetData data)
    {
        return data.Accounts
            .OrderBy(account => account.Name)
            .Select(BuildAccount)
            .ToList();
    }

    private static AccountDto BuildAccount(Account account)
    {
        return new AccountDto(
            account.Id,
            account.Name,
            account.Kind.ToString(),
            account.Balance,
            account.Balance.ToString("C"));
    }

    private static IReadOnlyList<CategoryGroupDto> BuildCategoryGroups(BudgetData data)
    {
        return data.CategoryGroups
            .OrderBy(group => group.Name)
            .Select(BuildCategoryGroup)
            .ToList();
    }

    private static CategoryGroupDto BuildCategoryGroup(CategoryGroup group)
    {
        return new CategoryGroupDto(
            group.Id,
            group.Name,
            group.Categories
                .OrderBy(category => category.Name)
                .Select(BuildCategory)
                .ToList());
    }

    private static CategoryDto BuildCategory(BudgetCategory category)
    {
        return new CategoryDto(category.Id, category.Name, category.MonthlyTarget);
    }

    private static TransactionDto BuildTransaction(TransactionListItem transaction)
    {
        return new TransactionDto(
            transaction.Id,
            transaction.Date,
            transaction.Date.ToString("yyyy-MM-dd"),
            transaction.AccountId,
            transaction.AccountName,
            transaction.CategoryId,
            transaction.CategoryName,
            transaction.Payee,
            transaction.Notes,
            transaction.Amount,
            transaction.Amount.ToString("C"));
    }

    private static decimal GetIncomeForMonth(BudgetData data, DateTime monthDate)
    {
        var monthStart = new DateTime(monthDate.Year, monthDate.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        return data.Transactions
            .Where(transaction => transaction.CategoryId is null)
            .Where(transaction => transaction.Date >= monthStart && transaction.Date < monthEnd)
            .Sum(transaction => transaction.Amount);
    }

    private static AccountKind ParseAccountKind(string rawKind)
    {
        if (Enum.TryParse<AccountKind>(rawKind, true, out var kind))
        {
            return kind;
        }

        throw new ArgumentException("Account kind is invalid.");
    }

    private static string RequireValue(string? value, string message)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        throw new ArgumentException(message);
    }

    private static string NormalizeMonthKey(string? monthKey)
    {
        return string.IsNullOrWhiteSpace(monthKey)
            ? BudgetCalculator.ToMonthKey(DateTime.Today)
            : monthKey;
    }

    private static string FormatSignedCurrency(decimal amount)
    {
        var absolute = Math.Abs(amount).ToString("C");
        return amount switch
        {
            > 0m => $"+{absolute}",
            < 0m => $"-{absolute}",
            _ => absolute
        };
    }

    private static bool TryParseBudgetAmount(string? rawValue, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        var expression = rawValue.Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        if (expression.Length == 0)
        {
            return true;
        }

        var index = 0;
        var sign = 1m;

        if (expression[index] is '+' or '-')
        {
            sign = expression[index] == '-' ? -1m : 1m;
            index++;
        }

        while (index < expression.Length)
        {
            var start = index;
            while (index < expression.Length && expression[index] is not ('+' or '-'))
            {
                index++;
            }

            if (start == index)
            {
                return false;
            }

            var token = expression[start..index];
            if (!decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                return false;
            }

            amount += sign * parsed;

            if (index >= expression.Length)
            {
                break;
            }

            sign = expression[index] == '-' ? -1m : 1m;
            index++;
            if (index >= expression.Length)
            {
                return false;
            }
        }

        return true;
    }
}
