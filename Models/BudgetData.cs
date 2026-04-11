namespace Kaboom.Models;

public sealed class BudgetData
{
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<Account> Accounts { get; set; } = [];
    public List<CategoryGroup> CategoryGroups { get; set; } = [];
    public List<TransactionRecord> Transactions { get; set; } = [];
    public List<MonthlyBudget> MonthlyBudgets { get; set; } = [];
}
