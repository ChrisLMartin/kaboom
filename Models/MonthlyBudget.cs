namespace Kaboom.Models;

public sealed class MonthlyBudget
{
    public string MonthKey { get; set; } = string.Empty;
    public List<CategoryAllocation> Allocations { get; set; } = [];
}
