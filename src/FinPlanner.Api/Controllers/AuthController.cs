using System.Security.Claims;
using FinPlanner.Api.Contracts;
using FinPlanner.Api.Data;
using FinPlanner.Api.Domain;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(FinPlannerDbContext dbContext, CurrentUserContext currentUser) : TenantControllerBase(currentUser)
{
    private const int MinPasswordLength = 8;
    private static readonly PasswordHasher<User> Hasher = new();

    // POST /api/auth/register — public. Creates a brand-new Family + its first User (Owner) + that family's
    // default Plan, atomically, and signs the new Owner in immediately. This is the only way a new family
    // enters the system (Part M decision #8) — there is no separate "create family" admin path.
    [HttpPost("register")]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FamilyName)) return BadRequest("Family name is required.");
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Name is required.");
        var normalizedEmail = NormalizeEmail(request.Email);
        if (!IsValidEmail(normalizedEmail)) return BadRequest("A valid email is required.");
        if (!IsValidPassword(request.Password)) return BadRequest($"Password must be at least {MinPasswordLength} characters.");
        if (await dbContext.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken))
            return BadRequest("An account with this email already exists.");

        var now = DateTime.UtcNow;
        var family = new Family { Name = request.FamilyName.Trim(), CreatedAt = now, UpdatedAt = now };
        dbContext.Families.Add(family);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Every family must have exactly one default Plan (Part B decision #2/#4) — Forecast, ATS, Compliance,
        // and Planning all fail with a 500 without one, so registration must create it, not just the Family.
        dbContext.Plans.Add(new Plan { FamilyId = family.Id, Name = "Normal", IsDefault = true, IsActive = true, CreatedAt = now, UpdatedAt = now });

        var owner = new User { FamilyId = family.Id, Name = request.Name.Trim(), Email = normalizedEmail, PasswordHash = string.Empty, Role = UserRole.Owner, CreatedAt = now, UpdatedAt = now };
        owner.PasswordHash = Hasher.HashPassword(owner, request.Password);
        dbContext.Users.Add(owner);
        await dbContext.SaveChangesAsync(cancellationToken);

        await SignInAsync(owner);
        return Created(string.Empty, ToResponse(owner));
    }

    [HttpPost("login")]
    public async Task<ActionResult<UserResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);
        // Same generic error regardless of whether the email exists or the password is wrong — avoids
        // letting a caller enumerate which emails have accounts (Part M decision #7).
        if (user is null || user.PasswordHash is null || Hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Unauthorized("Invalid email or password.");

        await SignInAsync(user);
        return Ok(ToResponse(user));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        // Server-side SignOutAsync clears/expires the authentication cookie via the response — no server-side
        // session store or per-session persistence is introduced (Part M decision #7).
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (RequireFamily(out _) is { } unauthorized) return unauthorized;
        var user = await dbContext.Users.SingleAsync(item => item.Id == CurrentUser.UserId, cancellationToken);
        if (user.PasswordHash is null || Hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            return BadRequest("Current password is incorrect.");
        if (!IsValidPassword(request.NewPassword)) return BadRequest($"Password must be at least {MinPasswordLength} characters.");

        // The cookie session stays valid after a password change (Part N decision #3) — there is no
        // server-side session store to revoke anyway (Part M decision #7), so forcing re-login here would not
        // actually invalidate any other device's session, only inconvenience the user changing their own.
        user.PasswordHash = Hasher.HashPassword(user, request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        if (RequireFamily(out _) is { } unauthorized) return unauthorized;
        var user = await dbContext.Users.AsNoTracking().SingleAsync(item => item.Id == CurrentUser.UserId, cancellationToken);
        return Ok(ToResponse(user));
    }

    private async Task SignInAsync(User user)
    {
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }

    internal static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    private static bool IsValidEmail(string email) => !string.IsNullOrWhiteSpace(email) && email.Contains('@') && email.Length <= 320;
    private static bool IsValidPassword(string password) => !string.IsNullOrEmpty(password) && password.Length >= MinPasswordLength;

    internal static UserResponse ToResponse(User user) => new(user.Id, user.Name, user.Role, user.FamilyId, user.Email, user.CreatedAt);
}
