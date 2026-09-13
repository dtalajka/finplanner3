using FinPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Data;

public static class SeedData
{
    public static async Task EnsureSeedDataAsync(FinPlannerDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (await dbContext.Families.AnyAsync(cancellationToken)) return;

        var now = DateTime.UtcNow;
        var family = new Family { Name = "Demo Family", CreatedAt = now, UpdatedAt = now };
        dbContext.Families.Add(family);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.Users.AddRange(
            new User { FamilyId = family.Id, Name = "Person A", Role = UserRole.Owner, CreatedAt = now, UpdatedAt = now },
            new User { FamilyId = family.Id, Name = "Person B", Role = UserRole.Member, CreatedAt = now, UpdatedAt = now });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
