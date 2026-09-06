using FinPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Data;

public sealed class FinPlannerDbContext(DbContextOptions<FinPlannerDbContext> options)
    : DbContext(options)
{
    public DbSet<FamilyAccount> FamilyAccounts => Set<FamilyAccount>();

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
    }
}