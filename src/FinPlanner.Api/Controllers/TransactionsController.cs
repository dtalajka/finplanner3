using FinPlanner.Api.Contracts;
using FinPlanner.Api.Data;
using FinPlanner.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Controllers;

[ApiController]
[Route("api/transactions")]
public sealed class TransactionsController(FinPlannerDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransactionResponse>>> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? type, CancellationToken cancellationToken)
    {
        var query = dbContext.ActualTransactions.AsNoTracking().AsQueryable();
        if (from is not null) query = query.Where(item => item.ActualDate >= from);
        if (to is not null) query = query.Where(item => item.ActualDate <= to);
        if (string.Equals(type, "Income", StringComparison.OrdinalIgnoreCase)) query = query.Where(item => item.FromAccountId == null && item.ToAccountId != null);
        if (string.Equals(type, "Expense", StringComparison.OrdinalIgnoreCase)) query = query.Where(item => item.FromAccountId != null && item.ToAccountId == null);
        var items = await query.OrderByDescending(item => item.ActualDate).ThenByDescending(item => item.Id).Select(item => new TransactionResponse(item.Id, item.ActualDate, item.Amount, item.FromAccountId, item.ToAccountId, item.CategoryId, item.PlannedTransactionId, item.Description, item.FromAccountId == null ? "INCOME" : item.ToAccountId == null ? "EXPENSE" : "TRANSFER")).ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<TransactionResponse>> Get(long id, CancellationToken cancellationToken)
    {
        var item = await dbContext.ActualTransactions.AsNoTracking().SingleOrDefaultAsync(transaction => transaction.Id == id, cancellationToken);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create(CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        var validation = await Validate(request.Date, request.Amount, request.FromAccountId, request.ToAccountId, request.CategoryId, cancellationToken);
        if (validation is not null) return validation;
        var now = DateTime.UtcNow;
        var item = new ActualTransaction { ActualDate = request.Date, Amount = request.Amount, FromAccountId = request.FromAccountId, ToAccountId = request.ToAccountId, CategoryId = request.CategoryId, PlannedTransactionId = request.PlannedTransactionId, Description = request.Description, CreatedAt = now, UpdatedAt = now };
        dbContext.ActualTransactions.Add(item); await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, ToResponse(item));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<TransactionResponse>> Update(long id, UpdateTransactionRequest request, CancellationToken cancellationToken)
    {
        var item = await dbContext.ActualTransactions.SingleOrDefaultAsync(transaction => transaction.Id == id, cancellationToken);
        if (item is null) return NotFound();
        var validation = await Validate(request.Date, request.Amount, request.FromAccountId, request.ToAccountId, request.CategoryId, cancellationToken);
        if (validation is not null) return validation;
        item.ActualDate = request.Date; item.Amount = request.Amount; item.FromAccountId = request.FromAccountId; item.ToAccountId = request.ToAccountId; item.CategoryId = request.CategoryId; item.PlannedTransactionId = request.PlannedTransactionId; item.Description = request.Description; item.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken); return Ok(ToResponse(item));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var item = await dbContext.ActualTransactions.FindAsync([id], cancellationToken);
        if (item is null) return NotFound(); dbContext.ActualTransactions.Remove(item); await dbContext.SaveChangesAsync(cancellationToken); return NoContent();
    }

    private async Task<ActionResult?> Validate(DateOnly date, decimal amount, long? from, long? to, long? categoryId, CancellationToken cancellationToken)
    {
        if (date == default) return BadRequest("Transaction date is required.");
        if (amount <= 0 || decimal.Round(amount, 2) != amount) return BadRequest("Amount must be positive and have at most two decimal places.");
        if (from is null && to is null) return BadRequest("At least one account is required.");
        if (from is not null && to is not null && from == to) return BadRequest("Source and destination accounts must differ.");
        var ids = new[] { from, to }.Where(id => id is not null).Select(id => id!.Value).Distinct().ToArray();
        if (await dbContext.Accounts.CountAsync(account => ids.Contains(account.Id) && account.Active, cancellationToken) != ids.Length) return BadRequest("All selected accounts must be active.");
        if (categoryId is not null && !await dbContext.Categories.AnyAsync(category => category.Id == categoryId && category.Active, cancellationToken)) return BadRequest("The selected category is not active.");
        if (from is not null && to is not null && categoryId is not null) return BadRequest("Transfers cannot have a category.");
        return null;
    }

    private static TransactionResponse ToResponse(ActualTransaction item) => new(item.Id, item.ActualDate, item.Amount, item.FromAccountId, item.ToAccountId, item.CategoryId, item.PlannedTransactionId, item.Description, item.FromAccountId is null ? "INCOME" : item.ToAccountId is null ? "EXPENSE" : "TRANSFER");
}
