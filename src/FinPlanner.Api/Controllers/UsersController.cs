using FinPlanner.Api.Contracts;
using FinPlanner.Api.Data;
using FinPlanner.Api.Domain;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(FinPlannerDbContext dbContext, CurrentUserContext currentUser) : TenantControllerBase(currentUser)
{
    private static readonly PasswordHasher<User> Hasher = new();

    // Family-scoped and authenticated (Part M closes the pre-existing hole where this returned every user of
    // every family with no authentication at all).
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> List(CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        return Ok(await dbContext.Users.AsNoTracking().Where(user => user.FamilyId == familyId).OrderBy(user => user.Name)
            .Select(user => new UserResponse(user.Id, user.Name, user.Role, user.FamilyId, user.Email, user.CreatedAt)).ToListAsync(cancellationToken));
    }

    // Adds another member to the CALLER's own family — never a way to join or create a different family.
    // Restricted to Owner (Part M decision #8) — the first place in the app where User.Role actually does
    // something, rather than being an inert field.
    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateFamilyMemberRequest request, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        if (CurrentUser.Role != UserRole.Owner) return StatusCode(StatusCodes.Status403Forbidden, "Only the family owner can add new members.");

        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Name is required.");
        var normalizedEmail = AuthController.NormalizeEmail(request.Email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || !normalizedEmail.Contains('@')) return BadRequest("A valid email is required.");
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 8) return BadRequest("Password must be at least 8 characters.");
        if (await dbContext.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken))
            return BadRequest("An account with this email already exists.");

        var now = DateTime.UtcNow;
        var member = new User { FamilyId = familyId, Name = request.Name.Trim(), Email = normalizedEmail, PasswordHash = string.Empty, Role = request.Role, CreatedAt = now, UpdatedAt = now };
        member.PasswordHash = Hasher.HashPassword(member, request.Password);
        dbContext.Users.Add(member);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(List), null, AuthController.ToResponse(member));
    }
}
