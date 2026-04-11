namespace Kaboom.Api;

public sealed record AuthUserDto(
    string Id,
    string Email,
    string DisplayName,
    string BudgetId,
    string BudgetName);

public sealed record AuthStatusDto(
    bool IsAuthenticated,
    AuthUserDto? User,
    IReadOnlyList<string> ExternalProviders);

public sealed record RegisterRequest(
    string Email,
    string Password,
    string? DisplayName);

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record AccountDto(
    string Id,
    string Name,
    string Kind,
    decimal Balance,
    string BalanceFormatted);

public sealed record AccountUpsertRequest(
    string Name,
    string Kind,
    decimal Balance);

public sealed record CategoryDto(
    string Id,
    string Name,
    decimal MonthlyTarget);

public sealed record CategoryGroupDto(
    string Id,
    string Name,
    IReadOnlyList<CategoryDto> Categories);

public sealed record AddCategoryRequest(
    string Name,
    string? GroupId,
    string? NewGroupName);

public sealed record RenameEntityRequest(
    string Name);

public sealed record BudgetMonthBreakdownDto(
    string Label,
    decimal Amount,
    string AmountFormatted,
    string Effect);

public sealed record BudgetMonthSummaryDto(
    string MonthKey,
    string Label,
    decimal TotalCash,
    decimal AvailableToBudget,
    string AvailableToBudgetFormatted,
    IReadOnlyList<BudgetMonthBreakdownDto> Breakdown,
    decimal Budgeted,
    string BudgetedFormatted,
    decimal Outflows,
    string OutflowsFormatted,
    decimal Balance,
    string BalanceFormatted);

public sealed record BudgetCellDto(
    string MonthKey,
    decimal Budgeted,
    string BudgetedInput,
    string BudgetedFormatted,
    decimal Outflows,
    string OutflowsFormatted,
    decimal Balance,
    string BalanceFormatted);

public sealed record BudgetCategoryRowDto(
    string CategoryId,
    string CategoryName,
    IReadOnlyList<BudgetCellDto> Cells);

public sealed record BudgetGroupRowDto(
    string GroupId,
    string GroupName,
    IReadOnlyList<BudgetCellDto> Totals,
    IReadOnlyList<BudgetCategoryRowDto> Categories);

public sealed record BudgetPageDto(
    string StartMonthKey,
    IReadOnlyList<BudgetMonthSummaryDto> Months,
    IReadOnlyList<BudgetGroupRowDto> Groups);

public sealed record BudgetAllocationUpdateDto(
    string MonthKey,
    string CategoryId,
    string Value);

public sealed record SaveBudgetRequest(
    string StartMonthKey,
    int MonthCount,
    IReadOnlyList<BudgetAllocationUpdateDto> Updates);

public sealed record TransactionDto(
    string Id,
    DateTime Date,
    string DateDisplay,
    string AccountId,
    string AccountName,
    string? CategoryId,
    string CategoryName,
    string Payee,
    string Notes,
    decimal Amount,
    string AmountFormatted);

public sealed record TransactionUpsertRequest(
    DateTime Date,
    string AccountId,
    string? CategoryId,
    string Payee,
    string? Notes,
    decimal Amount);

public sealed record TransactionsPageDto(
    IReadOnlyList<AccountDto> Accounts,
    IReadOnlyList<CategoryGroupDto> CategoryGroups,
    IReadOnlyList<TransactionDto> Transactions);

public sealed record ReportSpendingRowDto(
    string GroupName,
    decimal Spent,
    string SpentFormatted,
    decimal Remaining,
    string RemainingFormatted);

public sealed record ReportTargetRowDto(
    string GroupName,
    string CategoryName,
    decimal Target,
    string TargetFormatted,
    decimal Available,
    string AvailableFormatted,
    decimal ProgressRatio);

public sealed record ReportsPageDto(
    string MonthKey,
    string MonthLabel,
    IReadOnlyList<ReportSpendingRowDto> SpendingByGroup,
    IReadOnlyList<ReportTargetRowDto> Targets);
