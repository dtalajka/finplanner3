using System.Security.Claims;
using FinPlanner.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Security;

// Resolves CurrentUserContext from the authenticated cookie identity only (Part M decision #6 — the
// dual-mode X-User-Id fallback that existed during migration has been removed after end-to-end verification
// of the new /api/auth/* flow, closing the pre-authentication hole this phase set out to fix).
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, CurrentUserContext currentUser, FinPlannerDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claim = context.User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim is not null && long.TryParse(claim.Value, out var userId))
            {
                var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId);
                if (user is not null)
                {
                    currentUser.UserId = user.Id;
                    currentUser.FamilyId = user.FamilyId;
                    currentUser.Role = user.Role;
                }
            }
        }

        await next(context);
    }
}
