namespace Kaboom.Models;

public sealed class CategoryGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public List<BudgetCategory> Categories { get; set; } = [];
}
