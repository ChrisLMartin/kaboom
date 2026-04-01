namespace Spendwise.Models;

public sealed class BudgetCategory
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyTarget { get; set; }
}
