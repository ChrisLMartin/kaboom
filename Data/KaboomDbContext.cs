using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Kaboom.Models;

namespace Kaboom.Data;

public sealed class KaboomDbContext : IdentityDbContext<ApplicationUser>
{
    public KaboomDbContext(DbContextOptions<KaboomDbContext> options)
        : base(options)
    {
    }

    public DbSet<BudgetEntity> Budgets => Set<BudgetEntity>();
    public DbSet<BudgetMembershipEntity> BudgetMemberships => Set<BudgetMembershipEntity>();
    public DbSet<BudgetStateEntity> BudgetStates => Set<BudgetStateEntity>();
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
    public DbSet<CategoryGroupEntity> CategoryGroups => Set<CategoryGroupEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
    public DbSet<TransactionEntity> Transactions => Set<TransactionEntity>();
    public DbSet<MonthlyBudgetEntity> MonthlyBudgets => Set<MonthlyBudgetEntity>();
    public DbSet<CategoryAllocationEntity> CategoryAllocations => Set<CategoryAllocationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var accountKindConverter = new EnumToStringConverter<AccountKind>();

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(item => item.DisplayName).HasMaxLength(200);
            entity.Property(item => item.CreatedUtc).HasColumnName("created_utc");
            entity.HasMany(item => item.BudgetMemberships)
                .WithOne(item => item.User)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BudgetEntity>(entity =>
        {
            entity.ToTable("budgets");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Name).HasColumnName("name");
            entity.Property(item => item.CreatedUtc).HasColumnName("created_utc");
            entity.HasMany(item => item.Memberships)
                .WithOne(item => item.Budget)
                .HasForeignKey(item => item.BudgetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BudgetMembershipEntity>(entity =>
        {
            entity.ToTable("budget_memberships");
            entity.HasKey(item => new { item.BudgetId, item.UserId });
            entity.Property(item => item.BudgetId).HasColumnName("budget_id");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.Role).HasColumnName("role");
            entity.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        });

        modelBuilder.Entity<BudgetStateEntity>(entity =>
        {
            entity.ToTable("budget_state");
            entity.HasKey(item => item.BudgetId);
            entity.Property(item => item.BudgetId).HasColumnName("budget_id");
            entity.Property(item => item.CreatedUtc).HasColumnName("created_utc");
            entity.HasOne(item => item.Budget)
                .WithOne()
                .HasForeignKey<BudgetStateEntity>(item => item.BudgetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccountEntity>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.BudgetId).HasColumnName("budget_id");
            entity.Property(item => item.Name).HasColumnName("name");
            entity.Property(item => item.Kind).HasColumnName("kind").HasConversion(accountKindConverter);
            entity.Property(item => item.Balance).HasColumnName("balance").HasPrecision(18, 2);
            entity.HasOne(item => item.Budget)
                .WithMany(item => item.Accounts)
                .HasForeignKey(item => item.BudgetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CategoryGroupEntity>(entity =>
        {
            entity.ToTable("category_groups");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.BudgetId).HasColumnName("budget_id");
            entity.Property(item => item.Name).HasColumnName("name");
            entity.HasOne(item => item.Budget)
                .WithMany(item => item.CategoryGroups)
                .HasForeignKey(item => item.BudgetId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(item => item.Categories)
                .WithOne(item => item.Group)
                .HasForeignKey(item => item.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CategoryEntity>(entity =>
        {
            entity.ToTable("categories");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.BudgetId).HasColumnName("budget_id");
            entity.Property(item => item.GroupId).HasColumnName("group_id");
            entity.Property(item => item.Name).HasColumnName("name");
            entity.Property(item => item.MonthlyTarget).HasColumnName("monthly_target").HasPrecision(18, 2);
        });

        modelBuilder.Entity<TransactionEntity>(entity =>
        {
            entity.ToTable("transactions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.BudgetId).HasColumnName("budget_id");
            entity.Property(item => item.Date).HasColumnName("date").HasColumnType("date");
            entity.Property(item => item.AccountId).HasColumnName("account_id");
            entity.Property(item => item.CategoryId).HasColumnName("category_id");
            entity.Property(item => item.Payee).HasColumnName("payee");
            entity.Property(item => item.Notes).HasColumnName("notes");
            entity.Property(item => item.Amount).HasColumnName("amount").HasPrecision(18, 2);
            entity.HasOne(item => item.Budget)
                .WithMany(item => item.Transactions)
                .HasForeignKey(item => item.BudgetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MonthlyBudgetEntity>(entity =>
        {
            entity.ToTable("monthly_budgets");
            entity.HasKey(item => new { item.BudgetId, item.MonthKey });
            entity.Property(item => item.BudgetId).HasColumnName("budget_id");
            entity.Property(item => item.MonthKey).HasColumnName("month_key");
            entity.HasOne(item => item.Budget)
                .WithMany(item => item.MonthlyBudgets)
                .HasForeignKey(item => item.BudgetId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(item => item.Allocations)
                .WithOne(item => item.Month)
                .HasForeignKey(item => new { item.BudgetId, item.MonthKey })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CategoryAllocationEntity>(entity =>
        {
            entity.ToTable("category_allocations");
            entity.HasKey(item => new { item.BudgetId, item.MonthKey, item.CategoryId });
            entity.Property(item => item.BudgetId).HasColumnName("budget_id");
            entity.Property(item => item.MonthKey).HasColumnName("month_key");
            entity.Property(item => item.CategoryId).HasColumnName("category_id");
            entity.Property(item => item.AssignedAmount).HasColumnName("assigned_amount").HasPrecision(18, 2);
        });
    }
}

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<BudgetMembershipEntity> BudgetMemberships { get; set; } = [];
}

public sealed class BudgetEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public List<BudgetMembershipEntity> Memberships { get; set; } = [];
    public List<AccountEntity> Accounts { get; set; } = [];
    public List<CategoryGroupEntity> CategoryGroups { get; set; } = [];
    public List<TransactionEntity> Transactions { get; set; } = [];
    public List<MonthlyBudgetEntity> MonthlyBudgets { get; set; } = [];
}

public sealed class BudgetMembershipEntity
{
    public string BudgetId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = "Owner";
    public DateTime CreatedUtc { get; set; }
    public BudgetEntity? Budget { get; set; }
    public ApplicationUser? User { get; set; }
}

public sealed class BudgetStateEntity
{
    public string BudgetId { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public BudgetEntity? Budget { get; set; }
}

public sealed class AccountEntity
{
    public string Id { get; set; } = string.Empty;
    public string BudgetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountKind Kind { get; set; }
    public decimal Balance { get; set; }
    public BudgetEntity? Budget { get; set; }
}

public sealed class CategoryGroupEntity
{
    public string Id { get; set; } = string.Empty;
    public string BudgetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<CategoryEntity> Categories { get; set; } = [];
    public BudgetEntity? Budget { get; set; }
}

public sealed class CategoryEntity
{
    public string Id { get; set; } = string.Empty;
    public string BudgetId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyTarget { get; set; }
    public CategoryGroupEntity? Group { get; set; }
}

public sealed class TransactionEntity
{
    public string Id { get; set; } = string.Empty;
    public string BudgetId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public string Payee { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public BudgetEntity? Budget { get; set; }
}

public sealed class MonthlyBudgetEntity
{
    public string BudgetId { get; set; } = string.Empty;
    public string MonthKey { get; set; } = string.Empty;
    public List<CategoryAllocationEntity> Allocations { get; set; } = [];
    public BudgetEntity? Budget { get; set; }
}

public sealed class CategoryAllocationEntity
{
    public string BudgetId { get; set; } = string.Empty;
    public string MonthKey { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public decimal AssignedAmount { get; set; }
    public MonthlyBudgetEntity? Month { get; set; }
}
