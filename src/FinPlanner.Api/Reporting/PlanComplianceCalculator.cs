using FinPlanner.Api.Domain;
using FinPlanner.Api.Forecasting;

namespace FinPlanner.Api.Reporting;

public sealed record CategoryCompliance(
    long CategoryId, string CategoryName, CategoryType CategoryType,
    decimal PlannedAmount, decimal ActualAmount, decimal Variance, decimal? VariancePercent, string Favorability,
    int PlannedRealCount, int PlannedRecurringCount, int ActualMatchedCount, int ActualUnmatchedCount, double? AverageTimingVarianceDays);

public sealed record GoalCompliance(
    long GoalId, string GoalName, GoalPriority Priority,
    decimal PlannedContribution, decimal ActualContribution, decimal Variance, decimal? VariancePercent, string Favorability);

public sealed record ComplianceTotals(
    decimal PlannedIncome, decimal ActualIncome, decimal IncomeVariance,
    decimal PlannedExpense, decimal ActualExpense, decimal ExpenseVariance,
    decimal NetVariance, decimal? CategoriesOnPlanRatio);

public sealed record PeriodCompliance(
    DateOnly PeriodStart, DateOnly PeriodEnd, string Status,
    IReadOnlyList<CategoryCompliance> Categories, IReadOnlyList<GoalCompliance> Goals, ComplianceTotals Totals);

public sealed record PlanComplianceResult(
    DateOnly AsOf, DateOnly From, DateOnly To, IReadOnlyList<PeriodCompliance> Periods, ComplianceTotals Totals);

/// <summary>
/// Pure reporting/analytics layer over already-existing transaction/planning data (Part K). Never calls
/// ForecastCalculator or AvailableToSpendCalculator and never changes their semantics — the only shared
/// utility is the already-pure, date-only RecurringRuleExpander.Expand. No fuzzy matching: the only
/// actual-to-planned link used is the pre-existing, explicitly set ActualTransaction.PlannedTransactionId.
/// </summary>
public static class PlanComplianceCalculator
{
    private sealed record PlannedItem(DateOnly Date, long? CategoryId, long? GoalId, decimal Amount, bool IsRecurring);
    private sealed record ActualItem(DateOnly BucketDate, long? CategoryId, long? GoalId, decimal Amount, bool Matched, double? TimingDays);

    public static PlanComplianceResult Build(
        DateOnly asOf,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<Category> categories,
        IReadOnlyList<ActualTransaction> actualTransactions,
        IReadOnlyList<PlannedTransaction> plannedTransactions,
        IReadOnlyList<RecurringRule> recurringRules,
        IReadOnlyList<Goal> goals)
    {
        var categoryById = categories.ToDictionary(category => category.Id);
        var goalById = goals.ToDictionary(goal => goal.Id);

        // Same construction as ForecastCalculator/AvailableToSpendCalculator: a real row's own GoalId always
        // wins; this dictionary only derives a GoalId for virtual recurring occurrences. Lowest Id wins on collision.
        var goalIdByRecurringRuleId = goals.Where(goal => goal.Active && goal.RecurringRuleId is not null)
            .GroupBy(goal => goal.RecurringRuleId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(goal => goal.Id).First().Id);

        var shadowedRecurringDates = plannedTransactions.Where(item => item.RecurringRuleId is not null)
            .Select(item => (item.RecurringRuleId!.Value, item.PlannedDate)).ToHashSet();

        var plannedItems = new List<PlannedItem>();
        foreach (var item in plannedTransactions.Where(item => !item.Cancelled))
            plannedItems.Add(new PlannedItem(item.PlannedDate, item.CategoryId, item.GoalId, item.Amount, IsRecurring: false));

        foreach (var rule in recurringRules.Where(rule => rule.Active))
        {
            foreach (var date in RecurringRuleExpander.Expand(rule, from, to))
            {
                if (shadowedRecurringDates.Contains((rule.Id, date))) continue;
                var goalId = goalIdByRecurringRuleId.TryGetValue(rule.Id, out var id) ? id : (long?)null;
                plannedItems.Add(new PlannedItem(date, rule.CategoryId, goalId, rule.Amount, IsRecurring: true));
            }
        }

        // Includes cancelled rows deliberately: an actual explicitly matched to a since-cancelled planned row
        // must still be attributable to that row's period (Part K §4.5) even though it contributes 0 to plannedAmount.
        var plannedById = plannedTransactions.ToDictionary(item => item.Id);

        var actualItems = new List<ActualItem>();
        foreach (var actual in actualTransactions)
        {
            if (actual.PlannedTransactionId is { } plannedId && plannedById.TryGetValue(plannedId, out var plannedRow))
            {
                var timingDays = (double)(actual.ActualDate.DayNumber - plannedRow.PlannedDate.DayNumber);
                actualItems.Add(new ActualItem(plannedRow.PlannedDate, plannedRow.CategoryId, plannedRow.GoalId, actual.Amount, Matched: true, timingDays));
            }
            else
            {
                actualItems.Add(new ActualItem(actual.ActualDate, actual.CategoryId, actual.GoalId, actual.Amount, Matched: false, null));
            }
        }

        var periods = new List<PeriodCompliance>();
        var allCategoryCells = new List<CategoryCompliance>();

        foreach (var (periodStart, periodEnd) in MonthRanges(from, to))
        {
            var plannedInMonth = plannedItems.Where(item => item.Date >= periodStart && item.Date <= periodEnd).ToList();
            var actualInMonth = actualItems.Where(item => item.BucketDate >= periodStart && item.BucketDate <= periodEnd).ToList();

            var categoryIds = plannedInMonth.Where(item => item.CategoryId is not null).Select(item => item.CategoryId!.Value)
                .Concat(actualInMonth.Where(item => item.CategoryId is not null).Select(item => item.CategoryId!.Value))
                .Distinct();

            var categoryCells = new List<CategoryCompliance>();
            foreach (var categoryId in categoryIds)
            {
                if (!categoryById.TryGetValue(categoryId, out var category)) continue;
                var cell = BuildCategoryCell(category,
                    plannedInMonth.Where(item => item.CategoryId == categoryId).ToList(),
                    actualInMonth.Where(item => item.CategoryId == categoryId).ToList());
                categoryCells.Add(cell);
                allCategoryCells.Add(cell);
            }
            categoryCells = categoryCells.OrderBy(cell => cell.CategoryName).ToList();

            var goalIds = plannedInMonth.Where(item => item.GoalId is not null).Select(item => item.GoalId!.Value)
                .Concat(actualInMonth.Where(item => item.GoalId is not null).Select(item => item.GoalId!.Value))
                .Distinct();

            var goalCells = new List<GoalCompliance>();
            foreach (var goalId in goalIds)
            {
                if (!goalById.TryGetValue(goalId, out var goal)) continue;
                var plannedContribution = plannedInMonth.Where(item => item.GoalId == goalId).Sum(item => item.Amount);
                var actualContribution = actualInMonth.Where(item => item.GoalId == goalId).Sum(item => item.Amount);
                goalCells.Add(BuildGoalCell(goal, plannedContribution, actualContribution));
            }
            goalCells = goalCells.OrderBy(cell => cell.GoalName).ToList();

            var status = periodEnd < FirstDayOfMonth(asOf) ? "CLOSED" : periodStart > asOf ? "FUTURE" : "IN_PROGRESS";
            periods.Add(new PeriodCompliance(periodStart, periodEnd, status, categoryCells, goalCells, ComputeTotals(categoryCells)));
        }

        return new PlanComplianceResult(asOf, from, to, periods, ComputeTotals(allCategoryCells));
    }

    private static CategoryCompliance BuildCategoryCell(Category category, List<PlannedItem> planned, List<ActualItem> actual)
    {
        var plannedAmount = planned.Sum(item => item.Amount);
        var actualAmount = actual.Sum(item => item.Amount);
        var variance = actualAmount - plannedAmount;
        var variancePercent = plannedAmount != 0 ? Math.Round(variance / plannedAmount * 100, 2) : (decimal?)null;
        var matchedTimings = actual.Where(item => item.Matched && item.TimingDays is not null).Select(item => item.TimingDays!.Value).ToList();

        return new CategoryCompliance(
            category.Id, category.Name, category.Type,
            plannedAmount, actualAmount, variance, variancePercent, Favorability(category.Type, variance),
            planned.Count(item => !item.IsRecurring), planned.Count(item => item.IsRecurring),
            actual.Count(item => item.Matched), actual.Count(item => !item.Matched),
            matchedTimings.Count > 0 ? matchedTimings.Average() : (double?)null);
    }

    private static GoalCompliance BuildGoalCell(Goal goal, decimal plannedContribution, decimal actualContribution)
    {
        var variance = actualContribution - plannedContribution;
        var variancePercent = plannedContribution != 0 ? Math.Round(variance / plannedContribution * 100, 2) : (decimal?)null;
        // More contribution than planned is progress toward the goal, not overspending — INCOME-like polarity.
        var favorability = variance > 0 ? "FAVORABLE" : variance < 0 ? "UNFAVORABLE" : "ON_PLAN";
        return new GoalCompliance(goal.Id, goal.Name, goal.Priority, plannedContribution, actualContribution, variance, variancePercent, favorability);
    }

    private static string Favorability(CategoryType type, decimal variance)
    {
        if (variance == 0) return "ON_PLAN";
        var isOverPlan = variance > 0;
        return type == CategoryType.Income ? (isOverPlan ? "FAVORABLE" : "UNFAVORABLE") : (isOverPlan ? "UNFAVORABLE" : "FAVORABLE");
    }

    private static ComplianceTotals ComputeTotals(IReadOnlyList<CategoryCompliance> cells)
    {
        var incomeCells = cells.Where(cell => cell.CategoryType == CategoryType.Income).ToList();
        var expenseCells = cells.Where(cell => cell.CategoryType == CategoryType.Expense).ToList();
        var plannedIncome = incomeCells.Sum(cell => cell.PlannedAmount);
        var actualIncome = incomeCells.Sum(cell => cell.ActualAmount);
        var plannedExpense = expenseCells.Sum(cell => cell.PlannedAmount);
        var actualExpense = expenseCells.Sum(cell => cell.ActualAmount);

        var activeCells = cells.Where(cell => cell.PlannedAmount != 0 || cell.ActualAmount != 0).ToList();
        var ratio = activeCells.Count > 0
            ? Math.Round((decimal)activeCells.Count(cell => cell.Favorability != "UNFAVORABLE") / activeCells.Count, 4)
            : (decimal?)null;

        return new ComplianceTotals(
            plannedIncome, actualIncome, actualIncome - plannedIncome,
            plannedExpense, actualExpense, actualExpense - plannedExpense,
            (actualIncome - actualExpense) - (plannedIncome - plannedExpense), ratio);
    }

    private static DateOnly FirstDayOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private static IEnumerable<(DateOnly Start, DateOnly End)> MonthRanges(DateOnly from, DateOnly to)
    {
        var year = from.Year;
        var month = from.Month;
        while (new DateOnly(year, month, 1) <= to)
        {
            yield return (new DateOnly(year, month, 1), new DateOnly(year, month, DateTime.DaysInMonth(year, month)));
            month++;
            if (month > 12) { month = 1; year++; }
        }
    }
}
