using Microsoft.AspNetCore.Mvc.RazorPages;
using Spendwise.Services;

namespace Spendwise.Pages;

public sealed class IndexModel : PageModel
{
    private readonly IBudgetRepository _repository;
    private readonly BudgetCalculator _calculator;

    public IndexModel(IBudgetRepository repository, BudgetCalculator calculator)
    {
        _repository = repository;
        _calculator = calculator;
    }

    public DashboardViewModel Dashboard { get; private set; } = default!;

    public async Task OnGetAsync(string? month)
    {
        var data = await _repository.GetAsync();
        var monthKey = string.IsNullOrWhiteSpace(month) ? BudgetCalculator.ToMonthKey(DateTime.Today) : month;
        Dashboard = _calculator.BuildDashboard(data, monthKey);
    }
}
