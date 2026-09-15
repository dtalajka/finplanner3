using FinPlanner.Api.Contracts;
using FinPlanner.Api.Data;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Controllers;

[ApiController]
[Route("api/family")]
public sealed class FamilyController(FinPlannerDbContext dbContext, CurrentUserContext currentUser) : TenantControllerBase(currentUser)
{
    [HttpGet]
    public async Task<ActionResult<FamilyResponse>> Get(CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var family = await dbContext.Families.AsNoTracking().SingleAsync(item => item.Id == familyId, cancellationToken);
        return Ok(new FamilyResponse(family.Id, family.Name, family.DefaultMinimumReserve));
    }

    [HttpPut]
    public async Task<ActionResult<FamilyResponse>> Update(UpdateFamilyReserveRequest request, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        if (request.DefaultMinimumReserve < 0) return BadRequest("Default minimum reserve cannot be negative.");
        var family = await dbContext.Families.SingleAsync(item => item.Id == familyId, cancellationToken);
        family.DefaultMinimumReserve = request.DefaultMinimumReserve;
        family.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new FamilyResponse(family.Id, family.Name, family.DefaultMinimumReserve));
    }
}
