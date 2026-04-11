using Kaboom.Models;

namespace Kaboom.Services;

public static class DemoDataFactory
{
    public static BudgetData Create()
    {
        var everyday = new Account
        {
            Id = "acct-everyday",
            Name = "Everyday Checking",
            Kind = AccountKind.Checking,
            Balance = 4425.50m
        };

        var savings = new Account
        {
            Id = "acct-savings",
            Name = "Rainy Day Savings",
            Kind = AccountKind.Savings,
            Balance = 2800m
        };

        var groceries = new BudgetCategory { Id = "cat-groceries", Name = "Groceries", MonthlyTarget = 700m };
        var dining = new BudgetCategory { Id = "cat-dining", Name = "Dining Out", MonthlyTarget = 180m };
        var rent = new BudgetCategory { Id = "cat-rent", Name = "Rent", MonthlyTarget = 1850m };
        var utilities = new BudgetCategory { Id = "cat-utilities", Name = "Utilities", MonthlyTarget = 240m };
        var phone = new BudgetCategory { Id = "cat-phone", Name = "Phone", MonthlyTarget = 70m };
        var carMaintenance = new BudgetCategory { Id = "cat-car", Name = "Car Maintenance", MonthlyTarget = 95m };
        var holidays = new BudgetCategory { Id = "cat-holiday", Name = "Holiday Gifts", MonthlyTarget = 60m };
        var travel = new BudgetCategory { Id = "cat-travel", Name = "Weekend Escape", MonthlyTarget = 120m };

        var data = new BudgetData
        {
            Accounts = [everyday, savings],
            CategoryGroups =
            [
                new CategoryGroup
                {
                    Id = "grp-living",
                    Name = "Living Costs",
                    Categories = [rent, utilities, phone]
                },
                new CategoryGroup
                {
                    Id = "grp-daily",
                    Name = "Daily Life",
                    Categories = [groceries, dining]
                },
                new CategoryGroup
                {
                    Id = "grp-future",
                    Name = "True Expenses",
                    Categories = [carMaintenance, holidays]
                },
                new CategoryGroup
                {
                    Id = "grp-dreams",
                    Name = "Goals",
                    Categories = [travel]
                }
            ]
        };

        var currentMonth = BudgetCalculator.ToMonthKey(DateTime.Today);
        var lastMonth = BudgetCalculator.ToMonthKey(DateTime.Today.AddMonths(-1));

        data.MonthlyBudgets.Add(new MonthlyBudget
        {
            MonthKey = lastMonth,
            Allocations =
            [
                new CategoryAllocation { CategoryId = rent.Id, AssignedAmount = 1850m },
                new CategoryAllocation { CategoryId = utilities.Id, AssignedAmount = 240m },
                new CategoryAllocation { CategoryId = phone.Id, AssignedAmount = 70m },
                new CategoryAllocation { CategoryId = groceries.Id, AssignedAmount = 650m },
                new CategoryAllocation { CategoryId = dining.Id, AssignedAmount = 160m },
                new CategoryAllocation { CategoryId = carMaintenance.Id, AssignedAmount = 95m },
                new CategoryAllocation { CategoryId = holidays.Id, AssignedAmount = 60m },
                new CategoryAllocation { CategoryId = travel.Id, AssignedAmount = 100m }
            ]
        });

        data.MonthlyBudgets.Add(new MonthlyBudget
        {
            MonthKey = currentMonth,
            Allocations =
            [
                new CategoryAllocation { CategoryId = rent.Id, AssignedAmount = 1850m },
                new CategoryAllocation { CategoryId = utilities.Id, AssignedAmount = 240m },
                new CategoryAllocation { CategoryId = phone.Id, AssignedAmount = 70m },
                new CategoryAllocation { CategoryId = groceries.Id, AssignedAmount = 610m },
                new CategoryAllocation { CategoryId = dining.Id, AssignedAmount = 150m },
                new CategoryAllocation { CategoryId = carMaintenance.Id, AssignedAmount = 95m },
                new CategoryAllocation { CategoryId = holidays.Id, AssignedAmount = 60m },
                new CategoryAllocation { CategoryId = travel.Id, AssignedAmount = 120m }
            ]
        });

        data.Transactions.AddRange(
        [
            new TransactionRecord
            {
                Id = "txn-1",
                Date = DateTime.Today.AddDays(-20),
                AccountId = everyday.Id,
                CategoryId = groceries.Id,
                Payee = "Fresh Pantry",
                Notes = "Weekly shop",
                Amount = -154.27m
            },
            new TransactionRecord
            {
                Id = "txn-2",
                Date = DateTime.Today.AddDays(-18),
                AccountId = everyday.Id,
                CategoryId = rent.Id,
                Payee = "Riverside Realty",
                Notes = "April rent",
                Amount = -1850m
            },
            new TransactionRecord
            {
                Id = "txn-3",
                Date = DateTime.Today.AddDays(-14),
                AccountId = everyday.Id,
                CategoryId = dining.Id,
                Payee = "Bento & Co",
                Notes = "Dinner with friends",
                Amount = -42.50m
            },
            new TransactionRecord
            {
                Id = "txn-4",
                Date = DateTime.Today.AddDays(-11),
                AccountId = everyday.Id,
                CategoryId = utilities.Id,
                Payee = "City Utilities",
                Notes = "Electricity bill",
                Amount = -112.18m
            },
            new TransactionRecord
            {
                Id = "txn-5",
                Date = DateTime.Today.AddDays(-5),
                AccountId = savings.Id,
                CategoryId = null,
                Payee = "Salary",
                Notes = "Monthly pay",
                Amount = 3200m
            },
            new TransactionRecord
            {
                Id = "txn-6",
                Date = DateTime.Today.AddDays(-2),
                AccountId = everyday.Id,
                CategoryId = groceries.Id,
                Payee = "Open Market",
                Notes = "Fruit and essentials",
                Amount = -89.12m
            }
        ]);

        return data;
    }
}
