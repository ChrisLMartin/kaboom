namespace Kaboom.Models;

public sealed class TransactionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Date { get; set; } = DateTime.Today;
    public string AccountId { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public string Payee { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
