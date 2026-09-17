using FinPlanner.Api.Contracts;
using FinPlanner.Api.Data;
using FinPlanner.Api.Domain;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public sealed class AccountsController(FinPlannerDbContext dbContext, CurrentUserContext currentUser) : TenantControllerBase(currentUser)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountResponse>>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        return Ok(await dbContext.Accounts.AsNoTracking().Where(account => account.FamilyId == familyId && (includeInactive || account.Active)).OrderBy(account => account.Name)
            .Select(account => new AccountResponse(account.Id, account.Name, account.Currency, account.OpeningBalance, account.OwnerUserId, account.Active)).ToListAsync(cancellationToken));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AccountResponse>> Get(long id, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var account = await dbContext.Accounts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.FamilyId == familyId, cancellationToken);
        return account is null ? NotFound() : Ok(ToResponse(account));
    }

    [HttpPost]
    public async Task<ActionResult<AccountResponse>> Create(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        if (!Valid(request.Name, request.Currency, request.OpeningBalance, out var error)) return BadRequest(error);
        var ownerValidation = await ValidateOwnerAsync(familyId, request.OwnerUserId, cancellationToken);
        if (ownerValidation is not null) return ownerValidation;
        var now = DateTime.UtcNow;
        var account = new Account { FamilyId = familyId, Name = request.Name.Trim(), Currency = request.Currency.ToUpperInvariant(), OpeningBalance = request.OpeningBalance, OwnerUserId = request.OwnerUserId, CreatedAt = now, UpdatedAt = now };
        dbContext.Accounts.Add(account); await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = account.Id }, ToResponse(account));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AccountResponse>> Update(long id, UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var account = await dbContext.Accounts.SingleOrDefaultAsync(item => item.Id == id && item.FamilyId == familyId, cancellationToken);
        if (account is null) return NotFound();
        if (!Valid(request.Name, request.Currency, request.OpeningBalance, out var error)) return BadRequest(error);
        var ownerValidation = await ValidateOwnerAsync(familyId, request.OwnerUserId, cancellationToken);
        if (ownerValidation is not null) return ownerValidation;
        account.Name = request.Name.Trim(); account.Currency = request.Currency.ToUpperInvariant(); account.OpeningBalance = request.OpeningBalance; account.OwnerUserId = request.OwnerUserId; account.Active = request.Active; account.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken); return Ok(ToResponse(account));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var account = await dbContext.Accounts.SingleOrDefaultAsync(item => item.Id == id && item.FamilyId == familyId, cancellationToken);
        if (account is null) return NotFound(); account.Active = false; account.UpdatedAt = DateTime.UtcNow; await dbContext.SaveChangesAsync(cancellationToken); return NoContent();
    }

    private async Task<ActionResult?> ValidateOwnerAsync(long familyId, long? ownerUserId, CancellationToken cancellationToken)
    {
        if (ownerUserId is null) return null;
        var belongsToFamily = await dbContext.Users.AnyAsync(user => user.Id == ownerUserId && user.FamilyId == familyId, cancellationToken);
        return belongsToFamily ? null : BadRequest("The selected owner does not belong to this family.");
    }

    private static AccountResponse ToResponse(Account account) => new(account.Id, account.Name, account.Currency, account.OpeningBalance, account.OwnerUserId, account.Active);
    private static bool Valid(string name, string currency, decimal openingBalance, out string error)
    {
        if (string.IsNullOrWhiteSpace(name)) { error = "Account name is required."; return false; }
        if (currency.Length != 3 || currency.Any(character => !char.IsLetter(character))) { error = "Currency must be a three-letter code."; return false; }
        if (openingBalance < 0 || decimal.Round(openingBalance, 2) != openingBalance) { error = "Opening balance must have at most two decimal places."; return false; }
        error = string.Empty; return true;
    }
}
