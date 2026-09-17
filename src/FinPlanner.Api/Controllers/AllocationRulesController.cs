using FinPlanner.Api.Contracts;
using FinPlanner.Api.Data;
using FinPlanner.Api.Domain;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Controllers;

[ApiController]
[Route("api/allocation-rules")]
public sealed class AllocationRulesController(FinPlannerDbContext dbContext, CurrentUserContext currentUser) : TenantControllerBase(currentUser)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AllocationRuleResponse>>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var rules = await dbContext.ExpenseAllocationRules.AsNoTracking().Where(rule => rule.FamilyId == familyId && (includeInactive || rule.Active)).ToListAsync(cancellationToken);
        var shares = await dbContext.ExpenseAllocationShares.AsNoTracking().Where(share => share.FamilyId == familyId).ToListAsync(cancellationToken);
        return Ok(rules.Select(rule => ToResponse(rule, shares)).ToList());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AllocationRuleResponse>> Get(long id, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var rule = await dbContext.ExpenseAllocationRules.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.FamilyId == familyId, cancellationToken);
        if (rule is null) return NotFound();
        var shares = await dbContext.ExpenseAllocationShares.AsNoTracking().Where(share => share.AllocationRuleId == id).ToListAsync(cancellationToken);
        return Ok(ToResponse(rule, shares));
    }

    [HttpPost]
    public async Task<ActionResult<AllocationRuleResponse>> Create(CreateAllocationRuleRequest request, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var validation = await ValidateAsync(familyId, request.CategoryId, request.Method, request.Shares, excludingRuleId: null, cancellationToken);
        if (validation is not null) return validation;

        var now = DateTime.UtcNow;
        var rule = new ExpenseAllocationRule { FamilyId = familyId, CategoryId = request.CategoryId, Method = request.Method, CreatedAt = now, UpdatedAt = now };
        dbContext.ExpenseAllocationRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);

        var shares = request.Shares.Select(share => new ExpenseAllocationShare { FamilyId = familyId, AllocationRuleId = rule.Id, UserId = share.UserId, Percentage = share.Percentage, FixedAmount = share.FixedAmount }).ToList();
        dbContext.ExpenseAllocationShares.AddRange(shares);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = rule.Id }, ToResponse(rule, shares));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AllocationRuleResponse>> Update(long id, UpdateAllocationRuleRequest request, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var rule = await dbContext.ExpenseAllocationRules.SingleOrDefaultAsync(item => item.Id == id && item.FamilyId == familyId, cancellationToken);
        if (rule is null) return NotFound();
        var validation = await ValidateAsync(familyId, request.CategoryId, request.Method, request.Shares, excludingRuleId: id, cancellationToken);
        if (validation is not null) return validation;

        var existingShares = await dbContext.ExpenseAllocationShares.Where(share => share.AllocationRuleId == id).ToListAsync(cancellationToken);
        dbContext.ExpenseAllocationShares.RemoveRange(existingShares);

        rule.CategoryId = request.CategoryId; rule.Method = request.Method; rule.UpdatedAt = DateTime.UtcNow;
        var newShares = request.Shares.Select(share => new ExpenseAllocationShare { FamilyId = familyId, AllocationRuleId = id, UserId = share.UserId, Percentage = share.Percentage, FixedAmount = share.FixedAmount }).ToList();
        dbContext.ExpenseAllocationShares.AddRange(newShares);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(rule, newShares));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var rule = await dbContext.ExpenseAllocationRules.SingleOrDefaultAsync(item => item.Id == id && item.FamilyId == familyId, cancellationToken);
        if (rule is null) return NotFound();
        rule.Active = false; rule.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<ActionResult?> ValidateAsync(long familyId, long? categoryId, ExpenseAllocationMethod method, IReadOnlyList<AllocationShareRequest> shares, long? excludingRuleId, CancellationToken cancellationToken)
    {
        if (categoryId is not null && !await dbContext.Categories.AnyAsync(category => category.Id == categoryId && category.FamilyId == familyId, cancellationToken))
            return BadRequest("The selected category does not belong to this family.");

        if (shares.Count == 0) return BadRequest("At least one share is required.");
        if (shares.Select(share => share.UserId).Distinct().Count() != shares.Count) return BadRequest("Each user can appear at most once in the shares.");

        var userIds = shares.Select(share => share.UserId).Distinct().ToArray();
        if (await dbContext.Users.CountAsync(user => user.FamilyId == familyId && userIds.Contains(user.Id), cancellationToken) != userIds.Length)
            return BadRequest("All shares must reference users belonging to this family.");

        if (method == ExpenseAllocationMethod.FixedPercentage)
        {
            if (shares.Any(share => share.Percentage is null)) return BadRequest("Every share needs a percentage for the FixedPercentage method.");
            if (shares.Sum(share => share.Percentage!.Value) != 100m) return BadRequest("Percentages must sum to exactly 100.");
        }
        if (method == ExpenseAllocationMethod.FixedAmount && shares.Any(share => share.FixedAmount is null))
            return BadRequest("Every share needs a fixed amount for the FixedAmount method.");

        // At most one active rule per category (and at most one active family-default rule) — keeps
        // "which rule applies" unambiguous (Part L §7, mirrored by the DB's partial unique indexes).
        var conflicting = await dbContext.ExpenseAllocationRules.AsNoTracking()
            .Where(rule => rule.FamilyId == familyId && rule.Active && rule.CategoryId == categoryId && (excludingRuleId == null || rule.Id != excludingRuleId.Value))
            .AnyAsync(cancellationToken);
        if (conflicting) return BadRequest(categoryId is null ? "An active family-default allocation rule already exists." : "An active allocation rule already exists for this category.");

        return null;
    }

    private static AllocationRuleResponse ToResponse(ExpenseAllocationRule rule, IReadOnlyList<ExpenseAllocationShare> shares) => new(
        rule.Id, rule.CategoryId, rule.Method, rule.Active,
        shares.Where(share => share.AllocationRuleId == rule.Id).Select(share => new AllocationShareResponse(share.UserId, share.Percentage, share.FixedAmount)).ToList());
}
