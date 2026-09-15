using FinPlanner.Api.Contracts;
using FinPlanner.Api.Data;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Controllers;

[ApiController]
[Route("api/planning")]
public sealed class PlanningController(FinPlannerDbContext dbContext, CurrentUserContext currentUser) : TenantControllerBase(currentUser)
{
    [HttpPost("planned-transactions")]
    public async Task<ActionResult<PlannedTransactionResponse>> CreatePlanned(CreatePlannedTransactionRequest request, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var (planId, planError) = await ResolvePlanAsync(dbContext, familyId, request.PlanId, cancellationToken);
        if (planError is not null) return planError;
        var validation = await Validate(familyId, request.Amount, request.FromAccountId, request.ToAccountId, request.CategoryId, cancellationToken);
        if (validation is not null) return validation;
        var goalValidation = await ValidateGoalAsync(familyId, planId, request.GoalId, cancellationToken);
        if (goalValidation is not null) return goalValidation;
        var item = new Domain.PlannedTransaction { FamilyId = familyId, PlanId = planId, PlannedDate = request.Date, Amount = request.Amount, FromAccountId = request.FromAccountId, ToAccountId = request.ToAccountId, CategoryId = request.CategoryId, RecurringRuleId = request.RecurringRuleId, GoalId = request.GoalId, Description = request.Description, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        dbContext.PlannedTransactions.Add(item); await dbContext.SaveChangesAsync(cancellationToken);
        return Created($"/api/planning/planned-transactions/{item.Id}", ToResponse(item));
    }

    [HttpGet("planned-transactions")]
    public async Task<ActionResult<IReadOnlyList<PlannedTransactionResponse>>> PlannedTransactions([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? type, [FromQuery] long? planId, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var (resolvedPlanId, planError) = await ResolvePlanAsync(dbContext, familyId, planId, cancellationToken);
        if (planError is not null) return planError;
        var query = dbContext.PlannedTransactions.AsNoTracking().Where(item => item.FamilyId == familyId && item.PlanId == resolvedPlanId);
        if (from is not null) query = query.Where(item => item.PlannedDate >= from);
        if (to is not null) query = query.Where(item => item.PlannedDate <= to);
        if (string.Equals(type, "Income", StringComparison.OrdinalIgnoreCase)) query = query.Where(item => item.FromAccountId == null && item.ToAccountId != null);
        if (string.Equals(type, "Expense", StringComparison.OrdinalIgnoreCase)) query = query.Where(item => item.FromAccountId != null && item.ToAccountId == null);
        var items = await query.OrderBy(item => item.PlannedDate).ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpPut("planned-transactions/{id:long}")]
    public async Task<ActionResult<PlannedTransactionResponse>> UpdatePlanned(long id, UpdatePlannedTransactionRequest request, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var item = await dbContext.PlannedTransactions.SingleOrDefaultAsync(planned => planned.Id == id && planned.FamilyId == familyId, cancellationToken);
        if (item is null) return NotFound();
        var validation = await Validate(familyId, request.Amount, request.FromAccountId, request.ToAccountId, request.CategoryId, cancellationToken);
        if (validation is not null) return validation;
        var goalValidation = await ValidateGoalAsync(familyId, item.PlanId, request.GoalId, cancellationToken);
        if (goalValidation is not null) return goalValidation;
        item.PlannedDate = request.Date; item.Amount = request.Amount; item.FromAccountId = request.FromAccountId; item.ToAccountId = request.ToAccountId; item.CategoryId = request.CategoryId; item.GoalId = request.GoalId; item.Description = request.Description; item.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(item));
    }

    [HttpDelete("planned-transactions/{id:long}")]
    public async Task<IActionResult> DeletePlanned(long id, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var item = await dbContext.PlannedTransactions.SingleOrDefaultAsync(planned => planned.Id == id && planned.FamilyId == familyId, cancellationToken);
        if (item is null) return NotFound();
        if (await dbContext.ActualTransactions.AnyAsync(actual => actual.PlannedTransactionId == id, cancellationToken)) return BadRequest("Cannot delete a planned transaction that is already matched to an actual transaction.");
        dbContext.PlannedTransactions.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("recurring-rules")]
    public async Task<ActionResult<IReadOnlyList<RecurringRuleResponse>>> RecurringRules([FromQuery] long? planId, [FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var (resolvedPlanId, planError) = await ResolvePlanAsync(dbContext, familyId, planId, cancellationToken);
        if (planError is not null) return planError;
        var items = await dbContext.RecurringRules.AsNoTracking().Where(item => item.FamilyId == familyId && item.PlanId == resolvedPlanId && (includeInactive || item.Active))
            .OrderBy(item => item.Name).ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpPost("recurring-rules")]
    public async Task<ActionResult<RecurringRuleResponse>> CreateRecurring(CreateRecurringRuleRequest request, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var (planId, planError) = await ResolvePlanAsync(dbContext, familyId, request.PlanId, cancellationToken);
        if (planError is not null) return planError;
        var validation = await ValidateRecurring(familyId, request.Name, request.Amount, request.FromAccountId, request.ToAccountId, request.CategoryId, request.StartDate, request.EndDate, request.DayOfMonth, cancellationToken);
        if (validation is not null) return validation;
        var item = new Domain.RecurringRule { FamilyId = familyId, PlanId = planId, Name = request.Name.Trim(), Amount = request.Amount, FromAccountId = request.FromAccountId, ToAccountId = request.ToAccountId, CategoryId = request.CategoryId, Frequency = request.Frequency, DayOfMonth = request.DayOfMonth, StartDate = request.StartDate, EndDate = request.EndDate, Description = request.Description, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        dbContext.RecurringRules.Add(item); await dbContext.SaveChangesAsync(cancellationToken);
        return Created($"/api/planning/recurring-rules/{item.Id}", ToResponse(item));
    }

    [HttpPut("recurring-rules/{id:long}")]
    public async Task<ActionResult<RecurringRuleResponse>> UpdateRecurring(long id, UpdateRecurringRuleRequest request, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var item = await dbContext.RecurringRules.SingleOrDefaultAsync(rule => rule.Id == id && rule.FamilyId == familyId, cancellationToken);
        if (item is null) return NotFound();
        var validation = await ValidateRecurring(familyId, request.Name, request.Amount, request.FromAccountId, request.ToAccountId, request.CategoryId, request.StartDate, request.EndDate, request.DayOfMonth, cancellationToken);
        if (validation is not null) return validation;
        item.Name = request.Name.Trim(); item.Amount = request.Amount; item.FromAccountId = request.FromAccountId; item.ToAccountId = request.ToAccountId; item.CategoryId = request.CategoryId; item.Frequency = request.Frequency; item.DayOfMonth = request.DayOfMonth; item.StartDate = request.StartDate; item.EndDate = request.EndDate; item.Description = request.Description; item.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(item));
    }

    [HttpDelete("recurring-rules/{id:long}")]
    public async Task<IActionResult> DeleteRecurring(long id, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var item = await dbContext.RecurringRules.SingleOrDefaultAsync(rule => rule.Id == id && rule.FamilyId == familyId, cancellationToken);
        if (item is null) return NotFound();
        item.Active = false; item.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<ActionResult?> ValidateGoalAsync(long familyId, long planId, long? goalId, CancellationToken cancellationToken)
    {
        if (goalId is null) return null;
        var belongsToPlan = await dbContext.Goals.AnyAsync(goal => goal.Id == goalId && goal.FamilyId == familyId && goal.PlanId == planId, cancellationToken);
        return belongsToPlan ? null : BadRequest("The selected goal does not belong to this plan.");
    }

    private async Task<ActionResult?> Validate(long familyId, decimal amount, long? from, long? to, long? categoryId, CancellationToken cancellationToken)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount) return BadRequest("Amount must be positive and have at most two decimal places.");
        if (from is null && to is null) return BadRequest("At least one account is required.");
        if (from is not null && to is not null && from == to) return BadRequest("Source and destination accounts must differ.");
        if (from is not null && to is not null && categoryId is not null) return BadRequest("Transfers cannot have a category.");
        var ids = new[] { from, to }.Where(id => id is not null).Select(id => id!.Value).Distinct().ToArray();
        if (await dbContext.Accounts.CountAsync(account => account.FamilyId == familyId && ids.Contains(account.Id) && account.Active, cancellationToken) != ids.Length) return BadRequest("All selected accounts must be active.");
        if (categoryId is not null && !await dbContext.Categories.AnyAsync(category => category.FamilyId == familyId && category.Id == categoryId && category.Active, cancellationToken)) return BadRequest("The selected category is not active.");
        return null;
    }

    private async Task<ActionResult?> ValidateRecurring(long familyId, string name, decimal amount, long? from, long? to, long? categoryId, DateOnly startDate, DateOnly? endDate, short? dayOfMonth, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Rule name is required.");
        if (endDate is not null && endDate < startDate) return BadRequest("End date must be on or after start date.");
        if (dayOfMonth is not null && (dayOfMonth < 1 || dayOfMonth > 31)) return BadRequest("Day of month must be between 1 and 31.");
        return await Validate(familyId, amount, from, to, categoryId, cancellationToken);
    }

    private static PlannedTransactionResponse ToResponse(Domain.PlannedTransaction item) => new(item.Id, item.PlanId, item.PlannedDate, item.Amount, item.FromAccountId, item.ToAccountId, item.CategoryId, item.RecurringRuleId, item.Description, item.Cancelled, item.FromAccountId is null ? "INCOME" : item.ToAccountId is null ? "EXPENSE" : "TRANSFER", item.GoalId);
    private static RecurringRuleResponse ToResponse(Domain.RecurringRule item) => new(item.Id, item.PlanId, item.Name, item.Amount, item.FromAccountId, item.ToAccountId, item.CategoryId, item.Frequency, item.DayOfMonth, item.StartDate, item.EndDate, item.Active, item.Description, item.FromAccountId is null ? "INCOME" : item.ToAccountId is null ? "EXPENSE" : "TRANSFER");
}
