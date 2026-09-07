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
    public async Task<ActionResult<IReadOnlyList<AccountResponse>>> List(CancellationToken cancellationToken) => Ok(
        await dbContext.FamilyAccounts.AsNoTracking().Where(account => account.IsActive).OrderBy(account => account.Name)
            .Select(account => new AccountResponse(account.Id, account.Name, account.Type, account.Currency, account.IsActive))
            .ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<AccountResponse>> Create(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120) return BadRequest("Account name is required and must be 120 characters or fewer.");
        if (request.Currency.Length != 3) return BadRequest("Currency must be a three-letter code.");

        var account = new FamilyAccount { Id = Guid.NewGuid(), Name = request.Name.Trim(), Type = request.Type, Currency = request.Currency.ToUpperInvariant() };
        dbContext.FamilyAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Created($"/api/accounts/{account.Id}", new AccountResponse(account.Id, account.Name, account.Type, account.Currency, account.IsActive));
    }
}