namespace Kaboom.Models;

public sealed class Account
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public AccountKind Kind { get; set; }
    public decimal Balance { get; set; }
}
