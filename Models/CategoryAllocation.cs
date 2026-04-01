namespace Spendwise.Models;

public sealed class CategoryAllocation
{
    public string CategoryId { get; set; } = string.Empty;
    public decimal AssignedAmount { get; set; }
}
