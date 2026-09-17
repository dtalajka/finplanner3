using FinPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Data;

public sealed class FinPlannerDbContext(DbContextOptions<FinPlannerDbContext> options)
    : DbContext(options)
{
    public DbSet<Family> Families => Set<Family>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<RecurringRule> RecurringRules => Set<RecurringRule>();
    public DbSet<PlannedTransaction> PlannedTransactions => Set<PlannedTransaction>();
    public DbSet<ActualTransaction> ActualTransactions => Set<ActualTransaction>();
    public DbSet<ExpenseAllocationRule> ExpenseAllocationRules => Set<ExpenseAllocationRule>();
    public DbSet<ExpenseAllocationShare> ExpenseAllocationShares => Set<ExpenseAllocationShare>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("finplanner");

        modelBuilder.Entity<Family>(entity =>
        {
            entity.ToTable("family");
            entity.HasKey(family => family.Id);
            entity.Property(family => family.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(family => family.Name).HasColumnName("name").IsRequired();
            entity.Property(family => family.DefaultMinimumReserve).HasColumnName("default_minimum_reserve").HasPrecision(14, 2).HasDefaultValue(0m);
            entity.Property(family => family.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(family => family.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasCheckConstraint("family_default_minimum_reserve_chk", "default_minimum_reserve >= 0");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("app_user");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(user => user.FamilyId).HasColumnName("family_id").IsRequired();
            entity.Property(user => user.Name).HasColumnName("name").IsRequired();
            entity.Property(user => user.Email).HasColumnName("email").IsRequired();
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(user => user.Role).HasColumnName("role").HasMaxLength(10).HasConversion(value => value.ToString().ToUpperInvariant(), value => Enum.Parse<UserRole>(value, true));
            entity.Property(user => user.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(user => user.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasOne<Family>().WithMany().HasForeignKey(user => user.FamilyId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(user => user.FamilyId).HasDatabaseName("idx_app_user_family");
            // Global (not per-family) uniqueness — login happens before the family is known. Email is always
            // stored pre-normalized to lowercase (AuthController.NormalizeEmail), so a plain unique index here
            // already gives case-insensitive uniqueness without a citext extension or expression index.
            entity.HasIndex(user => user.Email).IsUnique().HasDatabaseName("uq_app_user_email");
            entity.HasCheckConstraint("app_user_role_chk", "role IN ('OWNER', 'MEMBER')");
        });

        modelBuilder.Entity<Plan>(entity =>
        {
            entity.ToTable("plan");
            entity.HasKey(plan => plan.Id);
            entity.Property(plan => plan.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(plan => plan.FamilyId).HasColumnName("family_id").IsRequired();
            entity.Property(plan => plan.Name).HasColumnName("name").IsRequired();
            entity.Property(plan => plan.Description).HasColumnName("description");
            entity.Property(plan => plan.IsDefault).HasColumnName("is_default").HasDefaultValue(false);
            entity.Property(plan => plan.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(plan => plan.MinimumReserve).HasColumnName("minimum_reserve").HasPrecision(14, 2);
            entity.Property(plan => plan.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(plan => plan.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasOne<Family>().WithMany().HasForeignKey(plan => plan.FamilyId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(plan => plan.FamilyId).IsUnique().HasFilter("is_default").HasDatabaseName("uq_plan_family_default");
            entity.HasCheckConstraint("plan_minimum_reserve_chk", "minimum_reserve IS NULL OR minimum_reserve >= 0");
        });

        modelBuilder.Entity<Goal>(entity =>
        {
            entity.ToTable("goal");
            entity.HasKey(goal => goal.Id);
            entity.Property(goal => goal.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(goal => goal.FamilyId).HasColumnName("family_id").IsRequired();
            entity.Property(goal => goal.PlanId).HasColumnName("plan_id").IsRequired();
            entity.Property(goal => goal.Name).HasColumnName("name").IsRequired();
            entity.Property(goal => goal.TargetAmount).HasColumnName("target_amount").HasPrecision(14, 2);
            entity.Property(goal => goal.TargetDate).HasColumnName("target_date");
            entity.Property(goal => goal.Priority).HasColumnName("priority").HasMaxLength(10).HasConversion(value => value.ToString().ToUpperInvariant(), value => Enum.Parse<GoalPriority>(value, true));
            entity.Property(goal => goal.RecurringRuleId).HasColumnName("recurring_rule_id");
            entity.Property(goal => goal.Description).HasColumnName("description");
            entity.Property(goal => goal.Active).HasColumnName("active").HasDefaultValue(true);
            entity.Property(goal => goal.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(goal => goal.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasOne<Family>().WithMany().HasForeignKey(goal => goal.FamilyId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<Plan>().WithMany().HasForeignKey(goal => goal.PlanId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<RecurringRule>().WithMany().HasForeignKey(goal => goal.RecurringRuleId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(goal => goal.FamilyId).HasDatabaseName("idx_goal_family");
            entity.HasIndex(goal => goal.PlanId).HasDatabaseName("idx_goal_plan");
            entity.HasCheckConstraint("goal_target_amount_chk", "target_amount > 0");
            entity.HasCheckConstraint("goal_priority_chk", "priority IN ('HARD', 'SOFT')");
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("account");
            entity.HasKey(account => account.Id);
            entity.Property(account => account.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(account => account.FamilyId).HasColumnName("family_id").IsRequired();
            entity.Property(account => account.Name).HasColumnName("name").IsRequired();
            entity.Property(account => account.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength().IsRequired();
            entity.Property(account => account.OpeningBalance).HasColumnName("opening_balance").HasPrecision(14, 2).HasDefaultValue(0m);
            entity.Property(account => account.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(account => account.Active).HasColumnName("active").HasDefaultValue(true);
            entity.Property(account => account.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(account => account.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasOne<Family>().WithMany().HasForeignKey(account => account.FamilyId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<User>().WithMany().HasForeignKey(account => account.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(account => account.FamilyId).HasDatabaseName("idx_account_family");
            entity.HasIndex(account => account.OwnerUserId).HasDatabaseName("idx_account_owner");
            entity.HasCheckConstraint("account_currency_chk", "currency ~ '^[A-Z]{3}$'");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("category");
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(category => category.FamilyId).HasColumnName("family_id").IsRequired();
            entity.Property(category => category.Name).HasColumnName("name").IsRequired();
            entity.Property(category => category.Type).HasColumnName("type").HasMaxLength(10).HasConversion(value => value.ToString().ToUpperInvariant(), value => Enum.Parse<CategoryType>(value, true));
            entity.Property(category => category.Active).HasColumnName("active").HasDefaultValue(true);
            entity.Property(category => category.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(category => category.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasOne<Family>().WithMany().HasForeignKey(category => category.FamilyId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(category => new { category.FamilyId, category.Name }).IsUnique().HasDatabaseName("category_name_uq");
            entity.HasCheckConstraint("category_type_chk", "type IN ('INCOME', 'EXPENSE')");
        });

        ConfigureRecurring(modelBuilder.Entity<RecurringRule>());
        ConfigurePlanned(modelBuilder.Entity<PlannedTransaction>());
        ConfigureActual(modelBuilder.Entity<ActualTransaction>());
        ConfigureAllocationRule(modelBuilder.Entity<ExpenseAllocationRule>());
        ConfigureAllocationShare(modelBuilder.Entity<ExpenseAllocationShare>());
    }

    private static void ConfigureAllocationRule(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ExpenseAllocationRule> entity)
    {
        entity.ToTable("expense_allocation_rule"); entity.HasKey(item => item.Id); entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(item => item.FamilyId).HasColumnName("family_id").IsRequired(); entity.Property(item => item.CategoryId).HasColumnName("category_id");
        entity.Property(item => item.Method).HasColumnName("method").HasMaxLength(20).HasConversion(value => MethodToColumn(value), value => MethodFromColumn(value));
        entity.Property(item => item.Active).HasColumnName("active").HasDefaultValue(true);
        entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        entity.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        entity.HasOne<Family>().WithMany().HasForeignKey(item => item.FamilyId).OnDelete(DeleteBehavior.NoAction);
        entity.HasOne<Category>().WithMany().HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.NoAction);
        entity.HasIndex(item => item.FamilyId).HasDatabaseName("idx_expense_allocation_rule_family");
        // At most one active rule per category, and at most one active family-default rule (category_id IS NULL) — resolves
        // the "which rule wins" ambiguity that Part L's I-A3 (category-specific beats family-default) assumes is unambiguous.
        entity.HasIndex(item => new { item.FamilyId, item.CategoryId }).IsUnique().HasFilter("active AND category_id IS NOT NULL").HasDatabaseName("uq_expense_allocation_rule_family_category");
        entity.HasIndex(item => item.FamilyId).IsUnique().HasFilter("active AND category_id IS NULL").HasDatabaseName("uq_expense_allocation_rule_family_default");
        entity.HasCheckConstraint("expense_allocation_rule_method_chk", "method IN ('FIXED_PERCENTAGE', 'EQUAL', 'INCOME_RATIO', 'FIXED_AMOUNT')");
    }

    private static void ConfigureAllocationShare(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ExpenseAllocationShare> entity)
    {
        entity.ToTable("expense_allocation_share"); entity.HasKey(item => item.Id); entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(item => item.FamilyId).HasColumnName("family_id").IsRequired(); entity.Property(item => item.AllocationRuleId).HasColumnName("allocation_rule_id").IsRequired();
        entity.Property(item => item.UserId).HasColumnName("user_id").IsRequired();
        entity.Property(item => item.Percentage).HasColumnName("percentage").HasPrecision(5, 2);
        entity.Property(item => item.FixedAmount).HasColumnName("fixed_amount").HasPrecision(14, 2);
        entity.HasOne<Family>().WithMany().HasForeignKey(item => item.FamilyId).OnDelete(DeleteBehavior.NoAction);
        entity.HasOne<ExpenseAllocationRule>().WithMany().HasForeignKey(item => item.AllocationRuleId).OnDelete(DeleteBehavior.NoAction);
        entity.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.NoAction);
        entity.HasIndex(item => item.AllocationRuleId).HasDatabaseName("idx_expense_allocation_share_rule");
        entity.HasIndex(item => new { item.AllocationRuleId, item.UserId }).IsUnique().HasDatabaseName("uq_expense_allocation_share_rule_user");
        entity.HasCheckConstraint("expense_allocation_share_percentage_chk", "percentage IS NULL OR (percentage >= 0 AND percentage <= 100)");
        entity.HasCheckConstraint("expense_allocation_share_fixed_amount_chk", "fixed_amount IS NULL OR fixed_amount >= 0");
    }

    private static string MethodToColumn(ExpenseAllocationMethod method) => method switch
    {
        ExpenseAllocationMethod.FixedPercentage => "FIXED_PERCENTAGE",
        ExpenseAllocationMethod.Equal => "EQUAL",
        ExpenseAllocationMethod.IncomeRatio => "INCOME_RATIO",
        ExpenseAllocationMethod.FixedAmount => "FIXED_AMOUNT",
        _ => throw new ArgumentOutOfRangeException(nameof(method))
    };

    private static ExpenseAllocationMethod MethodFromColumn(string value) => value switch
    {
        "FIXED_PERCENTAGE" => ExpenseAllocationMethod.FixedPercentage,
        "EQUAL" => ExpenseAllocationMethod.Equal,
        "INCOME_RATIO" => ExpenseAllocationMethod.IncomeRatio,
        "FIXED_AMOUNT" => ExpenseAllocationMethod.FixedAmount,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static void ConfigureRecurring(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<RecurringRule> entity)
    {
        entity.ToTable("recurring_rule"); entity.HasKey(item => item.Id); entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(item => item.FamilyId).HasColumnName("family_id").IsRequired(); entity.Property(item => item.PlanId).HasColumnName("plan_id").IsRequired(); entity.Property(item => item.Name).HasColumnName("name").IsRequired(); entity.Property(item => item.FromAccountId).HasColumnName("from_account_id"); entity.Property(item => item.ToAccountId).HasColumnName("to_account_id"); entity.Property(item => item.CategoryId).HasColumnName("category_id"); entity.Property(item => item.Amount).HasColumnName("amount").HasPrecision(14, 2); entity.Property(item => item.Frequency).HasColumnName("frequency").HasMaxLength(10).HasConversion(value => value.ToString().ToUpperInvariant(), value => Enum.Parse<RecurrenceFrequency>(value, true)); entity.Property(item => item.DayOfMonth).HasColumnName("day_of_month"); entity.Property(item => item.StartDate).HasColumnName("start_date"); entity.Property(item => item.EndDate).HasColumnName("end_date"); entity.Property(item => item.Active).HasColumnName("active").HasDefaultValue(true); entity.Property(item => item.Description).HasColumnName("description"); entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()"); entity.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        entity.HasOne<Domain.Family>().WithMany().HasForeignKey(item => item.FamilyId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Domain.Plan>().WithMany().HasForeignKey(item => item.PlanId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Account>().WithMany().HasForeignKey(item => item.FromAccountId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Account>().WithMany().HasForeignKey(item => item.ToAccountId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Category>().WithMany().HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.NoAction);
        entity.HasIndex(item => item.FamilyId).HasDatabaseName("idx_recurring_rule_family");
        entity.HasIndex(item => item.PlanId).HasDatabaseName("idx_recurring_rule_plan");
        entity.HasCheckConstraint("recurring_amount_chk", "amount > 0").HasCheckConstraint("recurring_frequency_chk", "frequency IN ('DAILY', 'WEEKLY', 'MONTHLY', 'YEARLY')").HasCheckConstraint("recurring_day_chk", "day_of_month IS NULL OR day_of_month BETWEEN 1 AND 31").HasCheckConstraint("recurring_dates_chk", "end_date IS NULL OR end_date >= start_date").HasCheckConstraint("recurring_account_chk", "from_account_id IS NOT NULL OR to_account_id IS NOT NULL").HasCheckConstraint("recurring_same_account_chk", "from_account_id IS NULL OR to_account_id IS NULL OR from_account_id <> to_account_id").HasCheckConstraint("recurring_transfer_category_chk", "(from_account_id IS NOT NULL AND to_account_id IS NOT NULL AND category_id IS NULL) OR NOT (from_account_id IS NOT NULL AND to_account_id IS NOT NULL)");
    }

    private static void ConfigurePlanned(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<PlannedTransaction> entity)
    {
        entity.ToTable("planned_transaction"); entity.HasKey(item => item.Id); entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd(); entity.Property(item => item.FamilyId).HasColumnName("family_id").IsRequired(); entity.Property(item => item.PlanId).HasColumnName("plan_id").IsRequired(); entity.Property(item => item.PlannedDate).HasColumnName("planned_date"); entity.Property(item => item.Amount).HasColumnName("amount").HasPrecision(14, 2); entity.Property(item => item.FromAccountId).HasColumnName("from_account_id"); entity.Property(item => item.ToAccountId).HasColumnName("to_account_id"); entity.Property(item => item.CategoryId).HasColumnName("category_id"); entity.Property(item => item.RecurringRuleId).HasColumnName("recurring_rule_id"); entity.Property(item => item.GoalId).HasColumnName("goal_id"); entity.Property(item => item.Description).HasColumnName("description"); entity.Property(item => item.Cancelled).HasColumnName("cancelled").HasDefaultValue(false); entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()"); entity.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        entity.HasOne<Domain.Family>().WithMany().HasForeignKey(item => item.FamilyId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Domain.Plan>().WithMany().HasForeignKey(item => item.PlanId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Account>().WithMany().HasForeignKey(item => item.FromAccountId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Account>().WithMany().HasForeignKey(item => item.ToAccountId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Category>().WithMany().HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<RecurringRule>().WithMany().HasForeignKey(item => item.RecurringRuleId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Goal>().WithMany().HasForeignKey(item => item.GoalId).OnDelete(DeleteBehavior.NoAction);
        entity.HasIndex(item => item.FamilyId).HasDatabaseName("idx_planned_transaction_family");
        entity.HasIndex(item => item.PlanId).HasDatabaseName("idx_planned_transaction_plan");
        entity.HasIndex(item => item.GoalId).HasDatabaseName("idx_planned_transaction_goal");
        entity.HasCheckConstraint("planned_amount_chk", "amount > 0").HasCheckConstraint("planned_account_chk", "from_account_id IS NOT NULL OR to_account_id IS NOT NULL").HasCheckConstraint("planned_same_account_chk", "from_account_id IS NULL OR to_account_id IS NULL OR from_account_id <> to_account_id").HasCheckConstraint("planned_transfer_category_chk", "(from_account_id IS NOT NULL AND to_account_id IS NOT NULL AND category_id IS NULL) OR NOT (from_account_id IS NOT NULL AND to_account_id IS NOT NULL)");
    }

    private static void ConfigureActual(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ActualTransaction> entity)
    {
        entity.ToTable("actual_transaction"); entity.HasKey(item => item.Id); entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd(); entity.Property(item => item.FamilyId).HasColumnName("family_id").IsRequired(); entity.Property(item => item.ActualDate).HasColumnName("actual_date"); entity.Property(item => item.Amount).HasColumnName("amount").HasPrecision(14, 2); entity.Property(item => item.FromAccountId).HasColumnName("from_account_id"); entity.Property(item => item.ToAccountId).HasColumnName("to_account_id"); entity.Property(item => item.CategoryId).HasColumnName("category_id"); entity.Property(item => item.PlannedTransactionId).HasColumnName("planned_transaction_id"); entity.Property(item => item.GoalId).HasColumnName("goal_id"); entity.Property(item => item.Description).HasColumnName("description"); entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()"); entity.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        entity.HasOne<Domain.Family>().WithMany().HasForeignKey(item => item.FamilyId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Account>().WithMany().HasForeignKey(item => item.FromAccountId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Account>().WithMany().HasForeignKey(item => item.ToAccountId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Category>().WithMany().HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<PlannedTransaction>().WithMany().HasForeignKey(item => item.PlannedTransactionId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Goal>().WithMany().HasForeignKey(item => item.GoalId).OnDelete(DeleteBehavior.NoAction);
        entity.HasIndex(item => item.FamilyId).HasDatabaseName("idx_actual_transaction_family");
        entity.HasIndex(item => item.GoalId).HasDatabaseName("idx_actual_transaction_goal");
        entity.HasIndex(item => item.PlannedTransactionId).IsUnique().HasFilter("planned_transaction_id IS NOT NULL").HasDatabaseName("uq_actual_transaction_planned");
        entity.HasCheckConstraint("actual_amount_chk", "amount > 0").HasCheckConstraint("actual_account_chk", "from_account_id IS NOT NULL OR to_account_id IS NOT NULL").HasCheckConstraint("actual_same_account_chk", "from_account_id IS NULL OR to_account_id IS NULL OR from_account_id <> to_account_id").HasCheckConstraint("actual_transfer_category_chk", "(from_account_id IS NOT NULL AND to_account_id IS NOT NULL AND category_id IS NULL) OR NOT (from_account_id IS NOT NULL AND to_account_id IS NOT NULL)");
    }
}