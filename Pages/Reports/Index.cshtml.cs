using Microsoft.AspNetCore.Mvc.RazorPages;
using Kaboom.Services;

namespace Kaboom.Pages.Reports;

public sealed class IndexModel : PageModel
{
    private readonly IBudgetRepository _repository;
    private readonly BudgetCalculator _calculator;

    public IndexModel(IBudgetRepository repository, BudgetCalculator calculator)
    {
        _repository = repository;
        _calculator = calculator;
    }

    public ReportViewModel Report { get; private set; } = default!;

    public async Task OnGetAsync(string? month)
    {
        var data = await _repository.GetAsync();
        var monthKey = string.IsNullOrWhiteSpace(month) ? BudgetCalculator.ToMonthKey(DateTime.Today) : month;
        Report = _calculator.BuildReport(data, monthKey);
    }
}
