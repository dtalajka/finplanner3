using FinPlanner.Api.Contracts;
using FinPlanner.Api.Data;
using FinPlanner.Api.Domain;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Controllers;

[ApiController]
[Route("api/goals")]
public sealed class GoalsController(FinPlannerDbContext dbContext, CurrentUserContext currentUser) : TenantControllerBase(currentUser)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GoalResponse>>> List([FromQuery] long? planId, [FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var (resolvedPlanId, planError) = await ResolvePlanAsync(dbContext, familyId, planId, cancellationToken);
        if (planError is not null) return planError;
        var goals = await dbContext.Goals.AsNoTracking().Where(goal => goal.FamilyId == familyId && goal.PlanId == resolvedPlanId && (includeInactive || goal.Active))
            .OrderBy(goal => goal.TargetDate).ToListAsync(cancellationToken);
        var responses = new List<GoalResponse>(goals.Count);
        foreach (var goal in goals) responses.Add(await ToResponseAsync(goal, cancellationToken));
        return Ok(responses);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<GoalResponse>> Get(long id, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var goal = await dbContext.Goals.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.FamilyId == familyId, cancellationToken);
        return goal is null ? NotFound() : Ok(await ToResponseAsync(goal, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<GoalResponse>> Create(CreateGoalRequest request, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var (planId, planError) = await ResolvePlanAsync(dbContext, familyId, request.PlanId, cancellationToken);
        if (planError is not null) return planError;
        var validation = await Validate(familyId, planId, request.Name, request.TargetAmount, request.RecurringRuleId, cancellationToken);
        if (validation is not null) return validation;
        var now = DateTime.UtcNow;
        var goal = new Goal { FamilyId = familyId, PlanId = planId, Name = request.Name.Trim(), TargetAmount = request.TargetAmount, TargetDate = request.TargetDate, Priority = request.Priority, RecurringRuleId = request.RecurringRuleId, Description = request.Description, CreatedAt = now, UpdatedAt = now };
        dbContext.Goals.Add(goal); await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = goal.Id }, await ToResponseAsync(goal, cancellationToken));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<GoalResponse>> Update(long id, UpdateGoalRequest request, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var goal = await dbContext.Goals.SingleOrDefaultAsync(item => item.Id == id && item.FamilyId == familyId, cancellationToken);
        if (goal is null) return NotFound();
        var validation = await Validate(familyId, goal.PlanId, request.Name, request.TargetAmount, request.RecurringRuleId, cancellationToken);
        if (validation is not null) return validation;
        goal.Name = request.Name.Trim(); goal.TargetAmount = request.TargetAmount; goal.TargetDate = request.TargetDate; goal.Priority = request.Priority; goal.RecurringRuleId = request.RecurringRuleId; goal.Description = request.Description; goal.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await ToResponseAsync(goal, cancellationToken));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var goal = await dbContext.Goals.SingleOrDefaultAsync(item => item.Id == id && item.FamilyId == familyId, cancellationToken);
        if (goal is null) return NotFound();
        goal.Active = false; goal.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<ActionResult?> Validate(long familyId, long planId, string name, decimal targetAmount, long? recurringRuleId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Goal name is required.");
        if (targetAmount <= 0 || decimal.Round(targetAmount, 2) != targetAmount) return BadRequest("Target amount must be positive and have at most two decimal places.");
        if (recurringRuleId is not null && !await dbContext.RecurringRules.AnyAsync(rule => rule.Id == recurringRuleId && rule.FamilyId == familyId && rule.PlanId == planId, cancellationToken))
            return BadRequest("The selected recurring rule does not belong to this plan.");
        return null;
    }

    private async Task<GoalResponse> ToResponseAsync(Goal goal, CancellationToken cancellationToken)
    {
        var currentFunding = await dbContext.ActualTransactions.Where(actual => actual.GoalId == goal.Id).SumAsync(actual => (decimal?)actual.Amount, cancellationToken) ?? 0m;
        var remainingAmount = Math.Max(0m, goal.TargetAmount - currentFunding);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var status = remainingAmount <= 0 ? "COMPLETE" : goal.TargetDate < today ? "OVERDUE" : "ACTIVE";
        var remainingPeriods = RemainingPeriods(today, goal.TargetDate);
        var requiredMonthlyContribution = remainingAmount <= 0 ? 0m : Math.Round(remainingAmount / remainingPeriods, 2);
        return new GoalResponse(goal.Id, goal.PlanId, goal.Name, goal.TargetAmount, goal.TargetDate, goal.Priority, goal.RecurringRuleId, goal.Description, goal.Active, currentFunding, remainingAmount, requiredMonthlyContribution, status);
    }

    private static int RemainingPeriods(DateOnly today, DateOnly targetDate)
    {
        if (targetDate <= today) return 1;
        var months = (targetDate.Year - today.Year) * 12 + (targetDate.Month - today.Month);
        return Math.Max(1, months);
    }
}
