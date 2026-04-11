using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Kaboom.Models;
using Kaboom.Services;
using System.Globalization;

namespace Kaboom.Pages.Budget;

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
    public Dictionary<string, string> BudgetedAmounts { get; set; } = [];

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

        var invalidKeys = new List<string>();

        foreach (var monthAssignments in BudgetedAmounts
                     .Where(entry => entry.Key.Contains('|'))
                     .GroupBy(entry => entry.Key.Split('|', 2)[0]))
        {
            var assignments = new Dictionary<string, decimal>();
            foreach (var entry in monthAssignments)
            {
                var categoryId = entry.Key.Split('|', 2)[1];
                if (!TryParseBudgetAmount(entry.Value, out var parsedAmount))
                {
                    invalidKeys.Add(entry.Key);
                    continue;
                }

                assignments[categoryId] = parsedAmount;
            }

            if (invalidKeys.Count > 0)
            {
                continue;
            }

            _calculator.SetAssignedAmounts(data, monthAssignments.Key, assignments);
        }

        if (invalidKeys.Count > 0)
        {
            if (IsAjaxRequest())
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return new JsonResult(new
                {
                    status = "invalid",
                    message = "Finish the budget math before saving."
                });
            }

            TempData["StatusMessage"] = "Finish the budget math before saving.";
            return RedirectToPage(new { month = startMonth });
        }

        await _repository.SaveAsync(data);

        if (IsAjaxRequest())
        {
            var months = BuildVisibleMonths(data, startMonth);
            return new JsonResult(new
            {
                status = "saved",
                savedAt = DateTime.Now.ToString("HH:mm:ss"),
                months = months.Select(month => new
                {
                    monthKey = month.MonthKey,
                    readyToAssign = month.ReadyToAssign,
                    readyToAssignFormatted = month.ReadyToAssign.ToString("C"),
                    budgeted = month.CategoryRows.Sum(row => row.Assigned),
                    budgetedFormatted = month.CategoryRows.Sum(row => row.Assigned).ToString("C"),
                    outflows = Math.Abs(month.CategoryRows.Where(row => row.Activity < 0).Sum(row => row.Activity)),
                    outflowsFormatted = Math.Abs(month.CategoryRows.Where(row => row.Activity < 0).Sum(row => row.Activity)).ToString("C"),
                    available = month.CategoryRows.Sum(row => row.Available),
                    availableFormatted = month.CategoryRows.Sum(row => row.Available).ToString("C"),
                    rows = month.CategoryRows.Select(row => new
                    {
                        categoryId = row.CategoryId,
                        assigned = row.Assigned,
                        assignedInput = row.Assigned.ToString("0.##", CultureInfo.InvariantCulture),
                        available = row.Available,
                        availableFormatted = row.Available.ToString("C")
                    }).ToList(),
                    groups = month.CategoryRows
                        .GroupBy(row => row.GroupId)
                        .Select(group => new
                        {
                            groupId = group.Key,
                            budgeted = group.Sum(row => row.Assigned),
                            budgetedFormatted = group.Sum(row => row.Assigned).ToString("C"),
                            outflows = group.Sum(row => row.Activity),
                            outflowsFormatted = group.Sum(row => row.Activity).ToString("C"),
                            balance = group.Sum(row => row.Available),
                            balanceFormatted = group.Sum(row => row.Available).ToString("C")
                        }).ToList()
                }).ToList()
            });
        }

        TempData["StatusMessage"] = "Budget and categories updated.";
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
        VisibleMonths = BuildVisibleMonths(data, startMonthKey);

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

    private IReadOnlyList<BudgetMonthViewModel> BuildVisibleMonths(BudgetData data, string startMonthKey)
    {
        var startMonth = BudgetCalculator.ParseMonthKey(startMonthKey);
        return Enumerable.Range(0, 3)
            .Select(offset => _calculator.BuildMonth(data, BudgetCalculator.ToMonthKey(startMonth.AddMonths(offset))))
            .ToList();
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseBudgetAmount(string? rawValue, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        var expression = rawValue.Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        if (expression.Length == 0)
        {
            return true;
        }

        var index = 0;
        var sign = 1m;

        if (expression[index] is '+' or '-')
        {
            sign = expression[index] == '-' ? -1m : 1m;
            index++;
        }

        while (index < expression.Length)
        {
            var start = index;
            while (index < expression.Length && expression[index] is not ('+' or '-'))
            {
                index++;
            }

            if (start == index)
            {
                return false;
            }

            var token = expression[start..index];
            if (!decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                return false;
            }

            amount += sign * parsed;

            if (index >= expression.Length)
            {
                break;
            }

            sign = expression[index] == '-' ? -1m : 1m;
            index++;
            if (index >= expression.Length)
            {
                return false;
            }
        }

        return true;
    }
}
