using FinPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Data;

public sealed class FinPlannerDbContext(DbContextOptions<FinPlannerDbContext> options)
    : DbContext(options)
{
    public DbSet<FamilyAccount> FamilyAccounts => Set<FamilyAccount>();

    public DbSet<FamilyTransaction> FamilyTransactions => Set<FamilyTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("finplanner");

        modelBuilder.Entity<FamilyAccount>(entity =>
        {
            entity.HasKey(account => account.Id);
            entity.Property(account => account.Name).HasMaxLength(120).IsRequired();
            entity.Property(account => account.Currency).HasMaxLength(3).IsRequired();
            entity.Property(account => account.Type).HasConversion<string>().HasMaxLength(30);
            entity.Property(account => account.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(account => account.Name).IsUnique();
        });

        modelBuilder.Entity<FamilyTransaction>(entity =>
        {
            entity.HasKey(transaction => transaction.Id);
            entity.Property(transaction => transaction.Description).HasMaxLength(200).IsRequired();
            entity.Property(transaction => transaction.Amount).HasPrecision(18, 2).IsRequired();
            entity.Property(transaction => transaction.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(transaction => transaction.TransactionDate).HasColumnType("date");
            entity.Property(transaction => transaction.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(transaction => transaction.UpdatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(transaction => transaction.FamilyAccount)
                .WithMany()
                .HasForeignKey(transaction => transaction.FamilyAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(transaction => new
            {
                transaction.FamilyAccountId,
                transaction.TransactionDate
            });
            entity.HasIndex(transaction => new
            {
                transaction.Type,
                transaction.TransactionDate
            });
        });
    }
}