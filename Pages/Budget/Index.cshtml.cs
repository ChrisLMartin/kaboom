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

    [BindProperty]
    public List<string> BufferCategoryIds { get; set; } = [];

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

    public async Task<IActionResult> OnPostAddGroupAsync(string startMonth, string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            TempData["StatusMessage"] = "Group name is required.";
            return RedirectToPage(new { month = startMonth });
        }

        var data = await _repository.GetAsync();
        data.CategoryGroups.Add(new CategoryGroup { Name = groupName.Trim() });
        await _repository.SaveAsync(data);
        TempData["StatusMessage"] = "Category group added.";
        return RedirectToPage(new { month = startMonth });
    }

    public async Task<IActionResult> OnPostAddCategoryAsync(string startMonth, string groupId, string categoryName, bool isBufferCategory)
    {
        var data = await _repository.GetAsync();
        var group = data.CategoryGroups.FirstOrDefault(item => item.Id == groupId);
        if (group is null || string.IsNullOrWhiteSpace(categoryName))
        {
            TempData["StatusMessage"] = "Choose a group and category name.";
            return RedirectToPage(new { month = startMonth });
        }

        group.Categories.Add(new BudgetCategory
        {
            Name = categoryName.Trim(),
            IsBufferCategory = isBufferCategory
        });

        await _repository.SaveAsync(data);
        TempData["StatusMessage"] = "Category added.";
        return RedirectToPage(new { month = startMonth });
    }

    private void ApplyCategoryEdits(BudgetData data)
    {
        var bufferSet = BufferCategoryIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

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

            category.IsBufferCategory = bufferSet.Contains(category.Id);
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
                        MonthlyTarget = category.MonthlyTarget,
                        IsBufferCategory = category.IsBufferCategory
                    })
                    .ToList()
            })
            .ToList();
    }
}
