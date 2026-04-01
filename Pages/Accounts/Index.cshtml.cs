using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Spendwise.Models;
using Spendwise.Services;

namespace Spendwise.Pages.Accounts;

public sealed class IndexModel : PageModel
{
    private readonly IBudgetRepository _repository;

    public IndexModel(IBudgetRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<Account> Accounts { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var data = await _repository.GetAsync();
        Accounts = data.Accounts.OrderBy(account => account.Name).ToList();
    }

    public async Task<IActionResult> OnPostAddAsync(string name, AccountKind kind, decimal balance)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["StatusMessage"] = "Account name is required.";
            return RedirectToPage();
        }

        var data = await _repository.GetAsync();
        data.Accounts.Add(new Account
        {
            Name = name.Trim(),
            Kind = kind,
            Balance = balance
        });

        await _repository.SaveAsync(data);
        TempData["StatusMessage"] = "Account added.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateBalanceAsync(string accountId, decimal balance)
    {
        var data = await _repository.GetAsync();
        var account = data.Accounts.FirstOrDefault(item => item.Id == accountId);
        if (account is null)
        {
            TempData["StatusMessage"] = "Account not found.";
            return RedirectToPage();
        }

        account.Balance = balance;
        await _repository.SaveAsync(data);
        TempData["StatusMessage"] = $"Updated {account.Name}.";
        return RedirectToPage();
    }
}
