using FinPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Data;

public sealed class FinPlannerDbContext(DbContextOptions<FinPlannerDbContext> options)
    : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<RecurringRule> RecurringRules => Set<RecurringRule>();
    public DbSet<PlannedTransaction> PlannedTransactions => Set<PlannedTransaction>();
    public DbSet<ActualTransaction> ActualTransactions => Set<ActualTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("finplanner");

        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("account");
            entity.HasKey(account => account.Id);
            entity.Property(account => account.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(account => account.Name).HasColumnName("name").IsRequired();
            entity.Property(account => account.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength().IsRequired();
            entity.Property(account => account.OpeningBalance).HasColumnName("opening_balance").HasPrecision(14, 2).HasDefaultValue(0m);
            entity.Property(account => account.Active).HasColumnName("active").HasDefaultValue(true);
            entity.Property(account => account.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(account => account.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasCheckConstraint("account_currency_chk", "currency ~ '^[A-Z]{3}$'");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("category");
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(category => category.Name).HasColumnName("name").IsRequired();
            entity.Property(category => category.Type).HasColumnName("type").HasMaxLength(10).HasConversion(value => value.ToString().ToUpperInvariant(), value => Enum.Parse<CategoryType>(value, true));
            entity.Property(category => category.Active).HasColumnName("active").HasDefaultValue(true);
            entity.Property(category => category.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(category => category.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(category => category.Name).IsUnique().HasDatabaseName("category_name_uq");
            entity.HasCheckConstraint("category_type_chk", "type IN ('INCOME', 'EXPENSE')");
        });

        ConfigureRecurring(modelBuilder.Entity<RecurringRule>());
        ConfigurePlanned(modelBuilder.Entity<PlannedTransaction>());
        ConfigureActual(modelBuilder.Entity<ActualTransaction>());
    }

    private static void ConfigureRecurring(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<RecurringRule> entity)
    {
        entity.ToTable("recurring_rule"); entity.HasKey(item => item.Id); entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(item => item.Name).HasColumnName("name").IsRequired(); entity.Property(item => item.FromAccountId).HasColumnName("from_account_id"); entity.Property(item => item.ToAccountId).HasColumnName("to_account_id"); entity.Property(item => item.CategoryId).HasColumnName("category_id"); entity.Property(item => item.Amount).HasColumnName("amount").HasPrecision(14, 2); entity.Property(item => item.Frequency).HasColumnName("frequency").HasMaxLength(10).HasConversion(value => value.ToString().ToUpperInvariant(), value => Enum.Parse<RecurrenceFrequency>(value, true)); entity.Property(item => item.DayOfMonth).HasColumnName("day_of_month"); entity.Property(item => item.StartDate).HasColumnName("start_date"); entity.Property(item => item.EndDate).HasColumnName("end_date"); entity.Property(item => item.Active).HasColumnName("active").HasDefaultValue(true); entity.Property(item => item.Description).HasColumnName("description"); entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()"); entity.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        entity.HasOne<Account>().WithMany().HasForeignKey(item => item.FromAccountId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Account>().WithMany().HasForeignKey(item => item.ToAccountId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Category>().WithMany().HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.NoAction);
        entity.HasCheckConstraint("recurring_amount_chk", "amount > 0").HasCheckConstraint("recurring_frequency_chk", "frequency IN ('DAILY', 'WEEKLY', 'MONTHLY', 'YEARLY')").HasCheckConstraint("recurring_day_chk", "day_of_month IS NULL OR day_of_month BETWEEN 1 AND 31").HasCheckConstraint("recurring_dates_chk", "end_date IS NULL OR end_date >= start_date").HasCheckConstraint("recurring_account_chk", "from_account_id IS NOT NULL OR to_account_id IS NOT NULL").HasCheckConstraint("recurring_same_account_chk", "from_account_id IS NULL OR to_account_id IS NULL OR from_account_id <> to_account_id").HasCheckConstraint("recurring_transfer_category_chk", "(from_account_id IS NOT NULL AND to_account_id IS NOT NULL AND category_id IS NULL) OR NOT (from_account_id IS NOT NULL AND to_account_id IS NOT NULL)");
    }

    private static void ConfigurePlanned(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<PlannedTransaction> entity)
    {
        entity.ToTable("planned_transaction"); entity.HasKey(item => item.Id); entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd(); entity.Property(item => item.PlannedDate).HasColumnName("planned_date"); entity.Property(item => item.Amount).HasColumnName("amount").HasPrecision(14, 2); entity.Property(item => item.FromAccountId).HasColumnName("from_account_id"); entity.Property(item => item.ToAccountId).HasColumnName("to_account_id"); entity.Property(item => item.CategoryId).HasColumnName("category_id"); entity.Property(item => item.RecurringRuleId).HasColumnName("recurring_rule_id"); entity.Property(item => item.Description).HasColumnName("description"); entity.Property(item => item.Cancelled).HasColumnName("cancelled").HasDefaultValue(false); entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()"); entity.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        entity.HasOne<Account>().WithMany().HasForeignKey(item => item.FromAccountId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Account>().WithMany().HasForeignKey(item => item.ToAccountId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Category>().WithMany().HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<RecurringRule>().WithMany().HasForeignKey(item => item.RecurringRuleId).OnDelete(DeleteBehavior.NoAction);
        entity.HasCheckConstraint("planned_amount_chk", "amount > 0").HasCheckConstraint("planned_account_chk", "from_account_id IS NOT NULL OR to_account_id IS NOT NULL").HasCheckConstraint("planned_same_account_chk", "from_account_id IS NULL OR to_account_id IS NULL OR from_account_id <> to_account_id").HasCheckConstraint("planned_transfer_category_chk", "(from_account_id IS NOT NULL AND to_account_id IS NOT NULL AND category_id IS NULL) OR NOT (from_account_id IS NOT NULL AND to_account_id IS NOT NULL)");
    }

    private static void ConfigureActual(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ActualTransaction> entity)
    {
        entity.ToTable("actual_transaction"); entity.HasKey(item => item.Id); entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd(); entity.Property(item => item.ActualDate).HasColumnName("actual_date"); entity.Property(item => item.Amount).HasColumnName("amount").HasPrecision(14, 2); entity.Property(item => item.FromAccountId).HasColumnName("from_account_id"); entity.Property(item => item.ToAccountId).HasColumnName("to_account_id"); entity.Property(item => item.CategoryId).HasColumnName("category_id"); entity.Property(item => item.PlannedTransactionId).HasColumnName("planned_transaction_id"); entity.Property(item => item.Description).HasColumnName("description"); entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()"); entity.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        entity.HasOne<Account>().WithMany().HasForeignKey(item => item.FromAccountId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Account>().WithMany().HasForeignKey(item => item.ToAccountId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<Category>().WithMany().HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.NoAction); entity.HasOne<PlannedTransaction>().WithMany().HasForeignKey(item => item.PlannedTransactionId).OnDelete(DeleteBehavior.NoAction);
        entity.HasCheckConstraint("actual_amount_chk", "amount > 0").HasCheckConstraint("actual_account_chk", "from_account_id IS NOT NULL OR to_account_id IS NOT NULL").HasCheckConstraint("actual_same_account_chk", "from_account_id IS NULL OR to_account_id IS NULL OR from_account_id <> to_account_id").HasCheckConstraint("actual_transfer_category_chk", "(from_account_id IS NOT NULL AND to_account_id IS NOT NULL AND category_id IS NULL) OR NOT (from_account_id IS NOT NULL AND to_account_id IS NOT NULL)");
    }
}