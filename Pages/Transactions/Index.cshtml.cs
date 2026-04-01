using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Spendwise.Models;
using Spendwise.Services;

namespace Spendwise.Pages.Transactions;

public sealed class IndexModel : PageModel
{
    private readonly IBudgetRepository _repository;
    private readonly BudgetCalculator _calculator;

    public IndexModel(IBudgetRepository repository, BudgetCalculator calculator)
    {
        _repository = repository;
        _calculator = calculator;
    }

    public IReadOnlyList<TransactionListItem> Transactions { get; private set; } = [];
    public IReadOnlyList<Account> Accounts { get; private set; } = [];
    public IReadOnlyList<CategoryGroup> Groups { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var data = await _repository.GetAsync();
        Transactions = _calculator.BuildTransactions(data);
        Accounts = data.Accounts.OrderBy(account => account.Name).ToList();
        Groups = data.CategoryGroups.OrderBy(group => group.Name).ToList();
    }

    public async Task<IActionResult> OnPostAddAsync(DateTime date, string accountId, string? categoryId, string payee, string notes, decimal amount)
    {
        var data = await _repository.GetAsync();
        var account = data.Accounts.FirstOrDefault(item => item.Id == accountId);
        if (account is null || string.IsNullOrWhiteSpace(payee))
        {
            TempData["StatusMessage"] = "Choose an account and payee.";
            return RedirectToPage();
        }

        data.Transactions.Add(new TransactionRecord
        {
            Date = date == default ? DateTime.Today : date,
            AccountId = accountId,
            CategoryId = string.IsNullOrWhiteSpace(categoryId) ? null : categoryId,
            Payee = payee.Trim(),
            Notes = notes?.Trim() ?? string.Empty,
            Amount = amount
        });

        account.Balance += amount;
        await _repository.SaveAsync(data);
        TempData["StatusMessage"] = "Transaction added.";
        return RedirectToPage();
    }
}
