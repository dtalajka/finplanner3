using FinPlanner.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Data;

public static class SeedData
{
    // Dev-only convenience credential for the auto-seeded demo family (never used if any family already
    // exists, e.g. the real dev DB). Documented here and in the final report — not appropriate for production,
    // where a real family should already exist (or be created via POST /api/auth/register) before this
    // seed path could ever trigger.
    private const string DevSeedPassword = "ChangeMe123!";

    public static async Task EnsureSeedDataAsync(FinPlannerDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (await dbContext.Families.AnyAsync(cancellationToken)) return;

        var hasher = new PasswordHasher<User>();
        var now = DateTime.UtcNow;
        var family = new Family { Name = "Demo Family", CreatedAt = now, UpdatedAt = now };
        dbContext.Families.Add(family);
        await dbContext.SaveChangesAsync(cancellationToken);

        var userA = new User { FamilyId = family.Id, Name = "Person A", Email = "person-a@example.local", PasswordHash = string.Empty, Role = UserRole.Owner, CreatedAt = now, UpdatedAt = now };
        userA.PasswordHash = hasher.HashPassword(userA, DevSeedPassword);
        var userB = new User { FamilyId = family.Id, Name = "Person B", Email = "person-b@example.local", PasswordHash = string.Empty, Role = UserRole.Member, CreatedAt = now, UpdatedAt = now };
        userB.PasswordHash = hasher.HashPassword(userB, DevSeedPassword);
        dbContext.Users.AddRange(userA, userB);
        dbContext.Plans.Add(new Plan { FamilyId = family.Id, Name = "Normal", IsDefault = true, IsActive = true, CreatedAt = now, UpdatedAt = now });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
