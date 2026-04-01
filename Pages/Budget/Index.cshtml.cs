using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Spendwise.Models;
using Spendwise.Services;

namespace Spendwise.Pages.Budget;

public sealed class IndexModel : PageModel
{
    private readonly IBudgetRepository _repository;
    private readonly BudgetCalculator _calculator;

    public IndexModel(IBudgetRepository repository, BudgetCalculator calculator)
    {
        _repository = repository;
        _calculator = calculator;
    }

    [BindProperty]
    public Dictionary<string, decimal> BudgetedAmounts { get; set; } = [];

    [BindProperty]
    public Dictionary<string, string> CategoryNames { get; set; } = [];

    [BindProperty]
    public Dictionary<string, string> GroupNames { get; set; } = [];

    public string StartMonthKey { get; private set; } = string.Empty;
    public IReadOnlyList<BudgetMonthViewModel> VisibleMonths { get; private set; } = [];
    public IReadOnlyList<CategoryGroup> CategoryGroups { get; private set; } = [];

    public async Task OnGetAsync(string? month)
    {
        var data = await _repository.GetAsync();
        var startMonthKey = string.IsNullOrWhiteSpace(month) ? BudgetCalculator.ToMonthKey(DateTime.Today) : month;
        LoadPage(data, startMonthKey);
    }

    public async Task<IActionResult> OnPostSaveAsync(string startMonth)
    {
        var data = await _repository.GetAsync();
        ApplyCategoryEdits(data);

        foreach (var monthAssignments in BudgetedAmounts
                     .Where(entry => entry.Key.Contains('|'))
                     .GroupBy(entry => entry.Key.Split('|', 2)[0]))
        {
            var assignments = monthAssignments.ToDictionary(
                entry => entry.Key.Split('|', 2)[1],
                entry => entry.Value);

            _calculator.SetAssignedAmounts(data, monthAssignments.Key, assignments);
        }

        await _repository.SaveAsync(data);

        if (IsAjaxRequest())
        {
            return new JsonResult(new
            {
                status = "saved",
                savedAt = DateTime.Now.ToString("HH:mm:ss")
            });
        }

        TempData["StatusMessage"] = "Budget and categories updated.";
        return RedirectToPage(new { month = startMonth });
    }

    public async Task<IActionResult> OnPostMoveAsync(string startMonth, string monthToMove, string fromCategoryId, string toCategoryId, decimal amount)
    {
        var data = await _repository.GetAsync();
        _calculator.MoveMoney(data, monthToMove, fromCategoryId, toCategoryId, amount);
        await _repository.SaveAsync(data);
        TempData["StatusMessage"] = "Moved money between categories.";
        return RedirectToPage(new { month = startMonth });
    }

    public async Task<IActionResult> OnPostAddCategoryAsync(string startMonth, string? groupId, string? newGroupName, string categoryName)
    {
        var data = await _repository.GetAsync();
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            TempData["StatusMessage"] = "Choose a category name.";
            return RedirectToPage(new { month = startMonth });
        }

        CategoryGroup? group = null;
        if (!string.IsNullOrWhiteSpace(newGroupName))
        {
            group = new CategoryGroup { Name = newGroupName.Trim() };
            data.CategoryGroups.Add(group);
        }
        else if (!string.IsNullOrWhiteSpace(groupId))
        {
            group = data.CategoryGroups.FirstOrDefault(item => item.Id == groupId);
        }

        if (group is null)
        {
            TempData["StatusMessage"] = "Choose an existing group or create a new one.";
            return RedirectToPage(new { month = startMonth });
        }

        group.Categories.Add(new BudgetCategory
        {
            Name = categoryName.Trim()
        });

        await _repository.SaveAsync(data);
        TempData["StatusMessage"] = string.IsNullOrWhiteSpace(newGroupName)
            ? "Category added."
            : "Category and new group added.";
        return RedirectToPage(new { month = startMonth });
    }

    private void ApplyCategoryEdits(BudgetData data)
    {
        foreach (var group in data.CategoryGroups)
        {
            if (GroupNames.TryGetValue(group.Id, out var groupName) && !string.IsNullOrWhiteSpace(groupName))
            {
                group.Name = groupName.Trim();
            }
        }

        foreach (var category in data.CategoryGroups.SelectMany(group => group.Categories))
        {
            if (CategoryNames.TryGetValue(category.Id, out var categoryName) && !string.IsNullOrWhiteSpace(categoryName))
            {
                category.Name = categoryName.Trim();
            }
        }
    }

    private void LoadPage(BudgetData data, string startMonthKey)
    {
        StartMonthKey = startMonthKey;

        var startMonth = BudgetCalculator.ParseMonthKey(startMonthKey);
        VisibleMonths = Enumerable.Range(0, 3)
            .Select(offset => _calculator.BuildMonth(data, BudgetCalculator.ToMonthKey(startMonth.AddMonths(offset))))
            .ToList();

        CategoryGroups = data.CategoryGroups
            .OrderBy(group => group.Name)
            .Select(group => new CategoryGroup
            {
                Id = group.Id,
                Name = group.Name,
                Categories = group.Categories
                    .OrderBy(category => category.Name)
                    .Select(category => new BudgetCategory
                    {
                        Id = category.Id,
                        Name = category.Name,
                        MonthlyTarget = category.MonthlyTarget
                    })
                    .ToList()
            })
            .ToList();
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }
}
