using FinPlanner.Api.Contracts;
using FinPlanner.Api.Data;
using FinPlanner.Api.Reporting;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Controllers;

[ApiController]
[Route("api/planning")]
public sealed class ComplianceController(FinPlannerDbContext dbContext, CurrentUserContext currentUser) : TenantControllerBase(currentUser)
{
    private const int DefaultMonths = 12;

    [HttpGet("compliance")]
    public async Task<ActionResult<PlanComplianceResponse>> Get([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        if (RequireFamily(out var familyId) is { } unauthorized) return unauthorized;
        // Always the family's default plan — actual-vs-plan compliance only makes sense against the plan
        // that is actually happening (Part E I-PA4 / Part J decision #2), never a what-if plan.
        var (planId, planError) = await ResolvePlanAsync(dbContext, familyId, null, cancellationToken);
        if (planError is not null) return planError;

        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        var rawTo = to ?? asOf;
        var rawFrom = from ?? rawTo.AddMonths(-(DefaultMonths - 1));
        var rangeFrom = new DateOnly(rawFrom.Year, rawFrom.Month, 1);
        var rangeTo = new DateOnly(rawTo.Year, rawTo.Month, DateTime.DaysInMonth(rawTo.Year, rawTo.Month));
        if (rangeFrom > rangeTo) return BadRequest("'from' must not be after 'to'.");

        var categories = await dbContext.Categories.AsNoTracking().Where(category => category.FamilyId == familyId).ToListAsync(cancellationToken);
        var recurringRules = await dbContext.RecurringRules.AsNoTracking().Where(rule => rule.FamilyId == familyId && rule.PlanId == planId && rule.Active).ToListAsync(cancellationToken);
        var goals = await dbContext.Goals.AsNoTracking().Where(goal => goal.FamilyId == familyId && goal.PlanId == planId).ToListAsync(cancellationToken);
        var plannedInWindow = await dbContext.PlannedTransactions.AsNoTracking()
            .Where(item => item.FamilyId == familyId && item.PlanId == planId && item.PlannedDate >= rangeFrom && item.PlannedDate <= rangeTo)
            .ToListAsync(cancellationToken);
        var plannedIdsInWindow = plannedInWindow.Select(item => item.Id).ToHashSet();
        var actuals = await dbContext.ActualTransactions.AsNoTracking()
            .Where(item => item.FamilyId == familyId && (
                (item.ActualDate >= rangeFrom && item.ActualDate <= rangeTo) ||
                (item.PlannedTransactionId != null && plannedIdsInWindow.Contains(item.PlannedTransactionId.Value))))
            .ToListAsync(cancellationToken);

        var result = PlanComplianceCalculator.Build(asOf, rangeFrom, rangeTo, categories, actuals, plannedInWindow, recurringRules, goals);
        return Ok(ToResponse(planId, result));
    }

    private static PlanComplianceResponse ToResponse(long planId, PlanComplianceResult result) => new(
        planId, result.AsOf, result.From, result.To,
        result.Periods.Select(ToResponse).ToList(),
        ToResponse(result.Totals));

    private static PeriodComplianceResponse ToResponse(PeriodCompliance period) => new(
        period.PeriodStart, period.PeriodEnd, period.Status,
        period.Categories.Select(ToResponse).ToList(),
        period.Goals.Select(ToResponse).ToList(),
        ToResponse(period.Totals));

    private static CategoryComplianceResponse ToResponse(CategoryCompliance item) => new(
        item.CategoryId, item.CategoryName, item.CategoryType, item.PlannedAmount, item.ActualAmount, item.Variance, item.VariancePercent, item.Favorability,
        item.PlannedRealCount, item.PlannedRecurringCount, item.ActualMatchedCount, item.ActualUnmatchedCount, item.AverageTimingVarianceDays);

    private static GoalComplianceResponse ToResponse(GoalCompliance item) => new(
        item.GoalId, item.GoalName, item.Priority, item.PlannedContribution, item.ActualContribution, item.Variance, item.VariancePercent, item.Favorability);

    private static ComplianceTotalsResponse ToResponse(ComplianceTotals totals) => new(
        totals.PlannedIncome, totals.ActualIncome, totals.IncomeVariance, totals.PlannedExpense, totals.ActualExpense, totals.ExpenseVariance, totals.NetVariance, totals.CategoriesOnPlanRatio);
}
