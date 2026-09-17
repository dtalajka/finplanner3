using FinPlanner.Api.Contracts;
using FinPlanner.Api.Data;
using FinPlanner.Api.Reporting;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Controllers;

[ApiController]
[Route("api/settlement")]
public sealed class SettlementController(FinPlannerDbContext dbContext, CurrentUserContext currentUser) : TenantControllerBase(currentUser)
{
    [HttpGet]
    public async Task<ActionResult<SettlementResponse>> Get([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;

        // Default range = the current calendar month (settlement is typically reconciled monthly,
        // unlike Compliance's 12-month default) — Part L §14.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rawTo = to ?? today;
        var rawFrom = from ?? rawTo;
        var rangeFrom = new DateOnly(rawFrom.Year, rawFrom.Month, 1);
        var rangeTo = new DateOnly(rawTo.Year, rawTo.Month, DateTime.DaysInMonth(rawTo.Year, rawTo.Month));
        if (rangeFrom > rangeTo) return BadRequest("'from' must not be after 'to'.");

        var users = await dbContext.Users.AsNoTracking().Where(user => user.FamilyId == familyId).ToListAsync(cancellationToken);
        var accounts = await dbContext.Accounts.AsNoTracking().Where(account => account.FamilyId == familyId).ToListAsync(cancellationToken);
        var categories = await dbContext.Categories.AsNoTracking().Where(category => category.FamilyId == familyId).ToListAsync(cancellationToken);
        var actuals = await dbContext.ActualTransactions.AsNoTracking()
            .Where(item => item.FamilyId == familyId && item.ActualDate >= rangeFrom && item.ActualDate <= rangeTo).ToListAsync(cancellationToken);
        var rules = await dbContext.ExpenseAllocationRules.AsNoTracking().Where(rule => rule.FamilyId == familyId && rule.Active).ToListAsync(cancellationToken);
        var shares = await dbContext.ExpenseAllocationShares.AsNoTracking().Where(share => share.FamilyId == familyId).ToListAsync(cancellationToken);

        var trailingIncomeByUserId = await ComputeTrailingIncomeAsync(familyId, accounts, rangeTo, cancellationToken);

        var result = SettlementCalculator.Build(rangeFrom, rangeTo, users, accounts, categories, actuals, rules, shares, trailingIncomeByUserId);
        return Ok(ToResponse(familyId, result));
    }

    // Each user's computed income = actual INCOME transactions into accounts they own, over a trailing
    // 3-month window ending at the settlement period's end date — Part E §7 (never manually entered).
    private async Task<Dictionary<long, decimal>> ComputeTrailingIncomeAsync(long familyId, IReadOnlyList<Domain.Account> accounts, DateOnly rangeTo, CancellationToken cancellationToken)
    {
        var ownerByAccountId = accounts.Where(account => account.OwnerUserId is not null).ToDictionary(account => account.Id, account => account.OwnerUserId!.Value);
        var incomeFrom = rangeTo.AddMonths(-3);
        var incomeActuals = await dbContext.ActualTransactions.AsNoTracking()
            .Where(item => item.FamilyId == familyId && item.FromAccountId == null && item.ToAccountId != null && item.ActualDate > incomeFrom && item.ActualDate <= rangeTo)
            .ToListAsync(cancellationToken);

        return incomeActuals.Where(item => item.ToAccountId is not null && ownerByAccountId.ContainsKey(item.ToAccountId.Value))
            .GroupBy(item => ownerByAccountId[item.ToAccountId!.Value])
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));
    }

    private static SettlementResponse ToResponse(long familyId, SettlementResult result) => new(
        familyId, result.From, result.To,
        result.Users.Select(user => new UserSettlementResponse(user.UserId, user.Name, user.TotalPaid, user.TotalExpectedContribution, user.Settlement)).ToList(),
        result.PoolContribution, result.UnattributedAmount,
        result.Categories.Select(category => new CategorySettlementResponse(category.CategoryId, category.CategoryName, category.Method, category.TotalAmount, category.PerUserExpected)).ToList());
}
