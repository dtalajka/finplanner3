using FinPlanner.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Security;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private const string UserIdHeader = "X-User-Id";

    public async Task InvokeAsync(HttpContext context, CurrentUserContext currentUser, FinPlannerDbContext dbContext)
    {
        if (context.Request.Headers.TryGetValue(UserIdHeader, out var value) && long.TryParse(value, out var userId))
        {
            var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId);
            if (user is not null)
            {
                currentUser.UserId = user.Id;
                currentUser.FamilyId = user.FamilyId;
            }
        }

        await next(context);
    }
}
