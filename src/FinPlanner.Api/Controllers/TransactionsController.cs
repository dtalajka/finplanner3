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
    public async Task<ActionResult<IReadOnlyList<TransactionResponse>>> List(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] FamilyTransactionType? type,
        CancellationToken cancellationToken)
    {
        var query = dbContext.FamilyTransactions.AsNoTracking().Include(transaction => transaction.FamilyAccount).AsQueryable();

        if (from is not null) query = query.Where(transaction => transaction.TransactionDate >= from);
        if (to is not null) query = query.Where(transaction => transaction.TransactionDate <= to);
        if (type is not null) query = query.Where(transaction => transaction.Type == type);

        var transactions = await query
            .OrderByDescending(transaction => transaction.TransactionDate)
            .ThenByDescending(transaction => transaction.CreatedAtUtc)
            .Select(transaction => ToResponse(transaction))
            .ToListAsync(cancellationToken);

        return Ok(transactions);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await dbContext.FamilyTransactions.AsNoTracking()
            .Include(item => item.FamilyAccount)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        return transaction is null ? NotFound() : Ok(ToResponse(transaction));
    }

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create(CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        var validation = await ValidateRequest(request.FamilyAccountId, request.Description, request.Amount, request.TransactionDate, cancellationToken);
        if (validation is not null) return validation;

        var now = DateTime.UtcNow;
        var transaction = new FamilyTransaction
        {
            Id = Guid.NewGuid(),
            FamilyAccountId = request.FamilyAccountId,
            Description = request.Description.Trim(),
            Amount = request.Amount,
            Type = request.Type,
            TransactionDate = request.TransactionDate,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.FamilyTransactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(transaction).Reference(item => item.FamilyAccount).LoadAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = transaction.Id }, ToResponse(transaction));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TransactionResponse>> Update(Guid id, UpdateTransactionRequest request, CancellationToken cancellationToken)
    {
        var transaction = await dbContext.FamilyTransactions.Include(item => item.FamilyAccount)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (transaction is null) return NotFound();

        var validation = await ValidateRequest(request.FamilyAccountId, request.Description, request.Amount, request.TransactionDate, cancellationToken);
        if (validation is not null) return validation;

        transaction.FamilyAccountId = request.FamilyAccountId;
        transaction.Description = request.Description.Trim();
        transaction.Amount = request.Amount;
        transaction.Type = request.Type;
        transaction.TransactionDate = request.TransactionDate;
        transaction.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(transaction).Reference(item => item.FamilyAccount).LoadAsync(cancellationToken);

        return Ok(ToResponse(transaction));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await dbContext.FamilyTransactions.FindAsync([id], cancellationToken);
        if (transaction is null) return NotFound();

        dbContext.FamilyTransactions.Remove(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<ActionResult?> ValidateRequest(Guid accountId, string description, decimal amount, DateOnly date, CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty) return BadRequest("An account is required.");
        if (string.IsNullOrWhiteSpace(description) || description.Length > 200) return BadRequest("Description is required and must be 200 characters or fewer.");
        if (amount <= 0 || decimal.Round(amount, 2) != amount) return BadRequest("Amount must be positive and have at most two decimal places.");
        if (date == default) return BadRequest("Transaction date is required.");
        if (!await dbContext.FamilyAccounts.AnyAsync(account => account.Id == accountId && account.IsActive, cancellationToken)) return BadRequest("The selected account does not exist or is inactive.");
        return null;
    }

    private static TransactionResponse ToResponse(FamilyTransaction transaction) => new(
        transaction.Id, transaction.FamilyAccountId, transaction.FamilyAccount.Name, transaction.Description,
        transaction.Amount, transaction.Type, transaction.TransactionDate, transaction.CreatedAtUtc, transaction.UpdatedAtUtc);
}