using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;

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
        return Unauthorized("Missing or unknown X-User-Id header.");
    }
}
