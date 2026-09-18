using FinPlanner.Api.Data;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Controllers;

public abstract class TenantControllerBase(CurrentUserContext currentUser) : ControllerBase
{
    protected CurrentUserContext CurrentUser { get; } = currentUser;

    protected ActionResult? RequireFamily(out long familyId)
    {
        if (CurrentUser.FamilyId is { } id)
        {
            familyId = id;
            return null;
        }

        familyId = default;
        return Unauthorized("Not authenticated.");
    }

    protected async Task<(long PlanId, ActionResult? Error)> ResolvePlanAsync(FinPlannerDbContext dbContext, long familyId, long? requestedPlanId, CancellationToken cancellationToken)
    {
        if (requestedPlanId is { } planId)
        {
            // Part N: a soft-deleted (IsActive=false) plan must not be resolvable via an explicit planId — otherwise
            // "deleting" a plan would only hide it from listings while its data could still be targeted directly.
            var belongsToFamily = await dbContext.Plans.AnyAsync(plan => plan.Id == planId && plan.FamilyId == familyId && plan.IsActive, cancellationToken);
            return belongsToFamily ? (planId, null) : (default, BadRequest("The selected plan does not belong to this family."));
        }
        var defaultPlan = await dbContext.Plans.AsNoTracking().SingleOrDefaultAsync(plan => plan.FamilyId == familyId && plan.IsDefault, cancellationToken);
        return defaultPlan is not null ? (defaultPlan.Id, null) : (default, StatusCode(500, "No default plan is configured for this family."));
    }
}
