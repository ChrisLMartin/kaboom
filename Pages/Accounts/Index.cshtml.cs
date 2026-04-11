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

    public IActionResult OnGetAsync()
    {
        return RedirectToPage("/Budget/Index");
    }

    public async Task<IActionResult> OnPostAddAsync(string name, AccountKind kind, decimal balance, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["StatusMessage"] = "Account name is required.";
            return RedirectToReturnUrl(returnUrl);
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
        return RedirectToReturnUrl(returnUrl);
    }

    public async Task<IActionResult> OnPostUpdateBalanceAsync(string accountId, decimal balance, string? returnUrl)
    {
        var data = await _repository.GetAsync();
        var account = data.Accounts.FirstOrDefault(item => item.Id == accountId);
        if (account is null)
        {
            TempData["StatusMessage"] = "Account not found.";
            return RedirectToReturnUrl(returnUrl);
        }

        account.Balance = balance;
        await _repository.SaveAsync(data);
        TempData["StatusMessage"] = $"Updated {account.Name}.";
        return RedirectToReturnUrl(returnUrl);
    }

    private IActionResult RedirectToReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToPage("/Budget/Index");
    }
}
