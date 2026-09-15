using FinPlanner.Api.Data;
using FinPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Tests;

internal static class TestDb
{
    public static FinPlannerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<FinPlannerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public static async Task<(Family Family, Plan DefaultPlan)> SeedFamilyWithDefaultPlanAsync(FinPlannerDbContext dbContext, string familyName = "Test Family")
    {
        var now = DateTime.UtcNow;
        var family = new Family { Name = familyName, CreatedAt = now, UpdatedAt = now };
        dbContext.Families.Add(family);
        await dbContext.SaveChangesAsync();

        var plan = new Plan { FamilyId = family.Id, Name = "Normal", IsDefault = true, IsActive = true, CreatedAt = now, UpdatedAt = now };
        dbContext.Plans.Add(plan);
        await dbContext.SaveChangesAsync();

        return (family, plan);
    }

    public static async Task<Account> AddAccountAsync(FinPlannerDbContext dbContext, long familyId, string name = "Joint")
    {
        var now = DateTime.UtcNow;
        var account = new Account { FamilyId = familyId, Name = name, Currency = "EUR", OpeningBalance = 0, Active = true, CreatedAt = now, UpdatedAt = now };
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();
        return account;
    }

    public static async Task<Plan> AddPlanAsync(FinPlannerDbContext dbContext, long familyId, string name = "Other Plan")
    {
        var now = DateTime.UtcNow;
        var plan = new Plan { FamilyId = familyId, Name = name, IsDefault = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        dbContext.Plans.Add(plan);
        await dbContext.SaveChangesAsync();
        return plan;
    }

    public static async Task<Goal> AddGoalAsync(FinPlannerDbContext dbContext, long familyId, long planId, string name = "Vacation", decimal targetAmount = 1000m, DateOnly? targetDate = null, GoalPriority priority = GoalPriority.Soft)
    {
        var now = DateTime.UtcNow;
        var goal = new Goal { FamilyId = familyId, PlanId = planId, Name = name, TargetAmount = targetAmount, TargetDate = targetDate ?? DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(6), Priority = priority, CreatedAt = now, UpdatedAt = now };
        dbContext.Goals.Add(goal);
        await dbContext.SaveChangesAsync();
        return goal;
    }
}
