using FinPlanner.Api.Contracts;
using FinPlanner.Api.Data;
using FinPlanner.Api.Domain;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Controllers;

[ApiController]
[Route("api/plans")]
public sealed class PlansController(FinPlannerDbContext dbContext, CurrentUserContext currentUser) : TenantControllerBase(currentUser)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlanResponse>>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        return Ok(await dbContext.Plans.AsNoTracking().Where(plan => plan.FamilyId == familyId && (includeInactive || plan.IsActive))
            .OrderByDescending(plan => plan.IsDefault).ThenBy(plan => plan.Name)
            .Select(plan => new PlanResponse(plan.Id, plan.Name, plan.Description, plan.IsDefault, plan.IsActive, plan.MinimumReserve)).ToListAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<PlanResponse>> Create(CreatePlanRequest request, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Plan name is required.");
        var now = DateTime.UtcNow;
        var plan = new Plan { FamilyId = familyId, Name = request.Name.Trim(), Description = request.Description, IsDefault = false, IsActive = true, CreatedAt = now, UpdatedAt = now };
        dbContext.Plans.Add(plan); await dbContext.SaveChangesAsync(cancellationToken);
        return Created($"/api/plans/{plan.Id}", ToResponse(plan));
    }

    [HttpPost("{id:long}/default")]
    public async Task<ActionResult<PlanResponse>> SetDefault(long id, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var target = await dbContext.Plans.SingleOrDefaultAsync(plan => plan.Id == id && plan.FamilyId == familyId, cancellationToken);
        if (target is null) return NotFound();
        if (!target.IsDefault)
        {
            var current = await dbContext.Plans.SingleOrDefaultAsync(plan => plan.FamilyId == familyId && plan.IsDefault, cancellationToken);
            if (current is not null) { current.IsDefault = false; current.UpdatedAt = DateTime.UtcNow; }
            target.IsDefault = true; target.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return Ok(ToResponse(target));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<PlanResponse>> Update(long id, UpdatePlanRequest request, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Plan name is required.");
        var plan = await dbContext.Plans.SingleOrDefaultAsync(item => item.Id == id && item.FamilyId == familyId, cancellationToken);
        if (plan is null) return NotFound();
        plan.Name = request.Name.Trim();
        plan.Description = request.Description;
        plan.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(plan));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var plan = await dbContext.Plans.SingleOrDefaultAsync(item => item.Id == id && item.FamilyId == familyId, cancellationToken);
        if (plan is null) return NotFound();
        // Soft-delete only (Part N) — RecurringRule/PlannedTransaction/Goal have required, NoAction FKs to
        // Plan.Id, so a hard delete would fail once any such row exists. Blocking the default plan keeps the
        // family's "always exactly one default plan" invariant intact (Forecast/ATS/Compliance depend on it).
        if (plan.IsDefault) return BadRequest("Cannot delete the default plan. Set another plan as default first.");
        plan.IsActive = false;
        plan.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:long}/reserve")]
    public async Task<ActionResult<PlanResponse>> SetReserve(long id, SetPlanReserveRequest request, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        if (request.MinimumReserve is < 0) return BadRequest("Minimum reserve cannot be negative.");
        var plan = await dbContext.Plans.SingleOrDefaultAsync(item => item.Id == id && item.FamilyId == familyId, cancellationToken);
        if (plan is null) return NotFound();
        plan.MinimumReserve = request.MinimumReserve;
        plan.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(plan));
    }

    private static PlanResponse ToResponse(Plan plan) => new(plan.Id, plan.Name, plan.Description, plan.IsDefault, plan.IsActive, plan.MinimumReserve);
}
