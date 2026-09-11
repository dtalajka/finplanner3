using FinPlanner.Api.Contracts;
using FinPlanner.Api.Data;
using FinPlanner.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public sealed class AccountsController(FinPlannerDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountResponse>>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken) => Ok(
        await dbContext.Accounts.AsNoTracking().Where(account => includeInactive || account.Active).OrderBy(account => account.Name)
            .Select(account => new AccountResponse(account.Id, account.Name, account.Currency, account.OpeningBalance, account.Active)).ToListAsync(cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AccountResponse>> Get(long id, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return account is null ? NotFound() : Ok(ToResponse(account));
    }

    [HttpPost]
    public async Task<ActionResult<AccountResponse>> Create(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        if (!Valid(request.Name, request.Currency, request.OpeningBalance, out var error)) return BadRequest(error);
        var now = DateTime.UtcNow;
        var account = new Account { Name = request.Name.Trim(), Currency = request.Currency.ToUpperInvariant(), OpeningBalance = request.OpeningBalance, CreatedAt = now, UpdatedAt = now };
        dbContext.Accounts.Add(account); await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = account.Id }, ToResponse(account));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AccountResponse>> Update(long id, UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (account is null) return NotFound();
        if (!Valid(request.Name, request.Currency, request.OpeningBalance, out var error)) return BadRequest(error);
        account.Name = request.Name.Trim(); account.Currency = request.Currency.ToUpperInvariant(); account.OpeningBalance = request.OpeningBalance; account.Active = request.Active; account.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken); return Ok(ToResponse(account));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (account is null) return NotFound(); account.Active = false; account.UpdatedAt = DateTime.UtcNow; await dbContext.SaveChangesAsync(cancellationToken); return NoContent();
    }

    private static AccountResponse ToResponse(Account account) => new(account.Id, account.Name, account.Currency, account.OpeningBalance, account.Active);
    private static bool Valid(string name, string currency, decimal openingBalance, out string error)
    {
        if (string.IsNullOrWhiteSpace(name)) { error = "Account name is required."; return false; }
        if (currency.Length != 3 || currency.Any(character => !char.IsLetter(character))) { error = "Currency must be a three-letter code."; return false; }
        if (openingBalance < 0 || decimal.Round(openingBalance, 2) != openingBalance) { error = "Opening balance must have at most two decimal places."; return false; }
        error = string.Empty; return true;
    }
}
