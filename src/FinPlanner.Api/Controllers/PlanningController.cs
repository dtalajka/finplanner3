using FinPlanner.Api.Contracts;
using FinPlanner.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Controllers;

[ApiController]
[Route("api/planning")]
public sealed class PlanningController(FinPlannerDbContext dbContext) : ControllerBase
{
    [HttpPost("planned-transactions")]
    public async Task<ActionResult<PlannedTransactionResponse>> CreatePlanned(CreatePlannedTransactionRequest request, CancellationToken cancellationToken)
    {
        var validation = await Validate(request.Amount, request.FromAccountId, request.ToAccountId, request.CategoryId, cancellationToken);
        if (validation is not null) return validation;
        var item = new Domain.PlannedTransaction { PlannedDate = request.Date, Amount = request.Amount, FromAccountId = request.FromAccountId, ToAccountId = request.ToAccountId, CategoryId = request.CategoryId, RecurringRuleId = request.RecurringRuleId, Description = request.Description, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        dbContext.PlannedTransactions.Add(item); await dbContext.SaveChangesAsync(cancellationToken);
        return Created($"/api/planning/planned-transactions/{item.Id}", new PlannedTransactionResponse(item.Id, item.PlannedDate, item.Amount, item.FromAccountId, item.ToAccountId, item.CategoryId, item.RecurringRuleId, item.Description, item.Cancelled));
    }

    [HttpGet("planned-transactions")]
    public async Task<ActionResult<IReadOnlyList<PlannedTransactionResponse>>> PlannedTransactions([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var query = dbContext.PlannedTransactions.AsNoTracking().AsQueryable();
        if (from is not null) query = query.Where(item => item.PlannedDate >= from);
        if (to is not null) query = query.Where(item => item.PlannedDate <= to);
        return Ok(await query.OrderBy(item => item.PlannedDate).Select(item => new PlannedTransactionResponse(item.Id, item.PlannedDate, item.Amount, item.FromAccountId, item.ToAccountId, item.CategoryId, item.RecurringRuleId, item.Description, item.Cancelled)).ToListAsync(cancellationToken));
    }

    [HttpGet("recurring-rules")]
    public async Task<ActionResult<IReadOnlyList<RecurringRuleResponse>>> RecurringRules(CancellationToken cancellationToken) => Ok(
        await dbContext.RecurringRules.AsNoTracking().OrderBy(item => item.Name).Select(item => new RecurringRuleResponse(item.Id, item.Name, item.Amount, item.Frequency, item.StartDate, item.EndDate, item.Active, item.Description)).ToListAsync(cancellationToken));

    [HttpPost("recurring-rules")]
    public async Task<ActionResult<RecurringRuleResponse>> CreateRecurring(CreateRecurringRuleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Rule name is required.");
        if (request.EndDate is not null && request.EndDate < request.StartDate) return BadRequest("End date must be on or after start date.");
        if (request.DayOfMonth is not null && (request.DayOfMonth < 1 || request.DayOfMonth > 31)) return BadRequest("Day of month must be between 1 and 31.");
        var validation = await Validate(request.Amount, request.FromAccountId, request.ToAccountId, request.CategoryId, cancellationToken);
        if (validation is not null) return validation;
        var item = new Domain.RecurringRule { Name = request.Name.Trim(), Amount = request.Amount, FromAccountId = request.FromAccountId, ToAccountId = request.ToAccountId, CategoryId = request.CategoryId, Frequency = request.Frequency, DayOfMonth = request.DayOfMonth, StartDate = request.StartDate, EndDate = request.EndDate, Description = request.Description, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        dbContext.RecurringRules.Add(item); await dbContext.SaveChangesAsync(cancellationToken);
        return Created($"/api/planning/recurring-rules/{item.Id}", new RecurringRuleResponse(item.Id, item.Name, item.Amount, item.Frequency, item.StartDate, item.EndDate, item.Active, item.Description));
    }

    private async Task<ActionResult?> Validate(decimal amount, long? from, long? to, long? categoryId, CancellationToken cancellationToken)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount) return BadRequest("Amount must be positive and have at most two decimal places.");
        if (from is null && to is null) return BadRequest("At least one account is required.");
        if (from is not null && to is not null && from == to) return BadRequest("Source and destination accounts must differ.");
        if (from is not null && to is not null && categoryId is not null) return BadRequest("Transfers cannot have a category.");
        var ids = new[] { from, to }.Where(id => id is not null).Select(id => id!.Value).Distinct().ToArray();
        if (await dbContext.Accounts.CountAsync(account => ids.Contains(account.Id) && account.Active, cancellationToken) != ids.Length) return BadRequest("All selected accounts must be active.");
        if (categoryId is not null && !await dbContext.Categories.AnyAsync(category => category.Id == categoryId && category.Active, cancellationToken)) return BadRequest("The selected category is not active.");
        return null;
    }
}