using FinPlanner.Api.Contracts;
using FinPlanner.Api.Data;
using FinPlanner.Api.Domain;
using FinPlanner.Api.Forecasting;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Controllers;

[ApiController]
[Route("api/planning")]
public sealed class ForecastController(FinPlannerDbContext dbContext, CurrentUserContext currentUser) : TenantControllerBase(currentUser)
{
    private const int MinHorizonMonths = 1;
    private const int MaxHorizonMonths = 60; // Part O: raised from 36 (Part H decision #8) to support a 5-year horizon selector in the UI.
    private const int DefaultHorizonMonths = 12;

    [HttpGet("forecast")]
    public async Task<ActionResult<ForecastResponse>> Get([FromQuery] long? planId, [FromQuery] int? horizonMonths, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var (resolvedPlanId, planError) = await ResolvePlanAsync(dbContext, familyId, planId, cancellationToken);
        if (planError is not null) return planError;

        var (asOf, horizonEnd) = ResolveHorizon(horizonMonths);
        var (accounts, actuals, planned, rules, goals) = await LoadForecastInputsAsync(familyId, resolvedPlanId, cancellationToken);

        var result = ForecastCalculator.Build(asOf, horizonEnd, accounts, actuals, planned, rules, goals);
        return Ok(ToResponse(resolvedPlanId, result));
    }

    [HttpGet("available-to-spend")]
    public async Task<ActionResult<AvailableToSpendResponse>> GetAvailableToSpend([FromQuery] long? planId, [FromQuery] int? horizonMonths, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        var (resolvedPlanId, planError) = await ResolvePlanAsync(dbContext, familyId, planId, cancellationToken);
        if (planError is not null) return planError;

        var (asOf, horizonEnd) = ResolveHorizon(horizonMonths);
        var plan = await dbContext.Plans.AsNoTracking().SingleAsync(item => item.Id == resolvedPlanId, cancellationToken);
        var family = await dbContext.Families.AsNoTracking().SingleAsync(item => item.Id == familyId, cancellationToken);
        var minimumReserve = plan.MinimumReserve ?? family.DefaultMinimumReserve;

        var (accounts, actuals, planned, rules, goals) = await LoadForecastInputsAsync(familyId, resolvedPlanId, cancellationToken);

        var result = AvailableToSpendCalculator.Build(asOf, horizonEnd, minimumReserve, accounts, actuals, planned, rules, goals);
        return Ok(ToResponse(resolvedPlanId, result));
    }

    private static (DateOnly AsOf, DateOnly HorizonEnd) ResolveHorizon(int? horizonMonths)
    {
        var months = Math.Clamp(horizonMonths ?? DefaultHorizonMonths, MinHorizonMonths, MaxHorizonMonths);
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        return (asOf, asOf.AddMonths(months));
    }

    private async Task<(List<Account> Accounts, List<ActualTransaction> Actuals, List<PlannedTransaction> Planned, List<RecurringRule> Rules, List<Goal> Goals)> LoadForecastInputsAsync(
        long familyId, long planId, CancellationToken cancellationToken)
    {
        var accounts = await dbContext.Accounts.AsNoTracking().Where(account => account.FamilyId == familyId && account.Active).ToListAsync(cancellationToken);
        var actuals = await dbContext.ActualTransactions.AsNoTracking().Where(item => item.FamilyId == familyId).ToListAsync(cancellationToken);
        var planned = await dbContext.PlannedTransactions.AsNoTracking().Where(item => item.FamilyId == familyId && item.PlanId == planId).ToListAsync(cancellationToken);
        var rules = await dbContext.RecurringRules.AsNoTracking().Where(item => item.FamilyId == familyId && item.PlanId == planId).ToListAsync(cancellationToken);
        var goals = await dbContext.Goals.AsNoTracking().Where(item => item.FamilyId == familyId && item.PlanId == planId).ToListAsync(cancellationToken);
        return (accounts, actuals, planned, rules, goals);
    }

    private static ForecastResponse ToResponse(long planId, ForecastResult result) => new(
        planId,
        result.AsOf,
        result.HorizonEnd,
        result.Accounts.Select(account => new ForecastAccountResponse(account.AccountId, account.Name, account.CurrentBalance)).ToList(),
        result.Events.Select(item => new ForecastEventResponse(item.Date, item.Amount, item.FromAccountId, item.ToAccountId, item.CategoryId, item.GoalId, item.TransactionType, item.Source, item.Status, item.PlannedTransactionId, item.RecurringRuleId, item.Description)).ToList(),
        result.Trajectory.Select(point => new ForecastTrajectoryPointResponse(point.Date, point.AccountBalances, point.TotalBalance)).ToList(),
        result.MinimumFutureBalance,
        result.MinimumFutureBalanceDate,
        result.NegativeBalanceRisk);

    private static AvailableToSpendResponse ToResponse(long planId, AvailableToSpendResult result) => new(
        planId,
        result.AsOf,
        result.HorizonEnd,
        result.MinimumReserve,
        result.ForecastMinBalance,
        result.ForecastMinBalanceDate,
        result.AvailableToSpend,
        result.ReserveShortfall,
        result.PlanStatus,
        result.Goals.Select(goal => new GoalFeasibilityResponse(goal.GoalId, goal.Name, goal.Priority, goal.Status)).ToList());
}
