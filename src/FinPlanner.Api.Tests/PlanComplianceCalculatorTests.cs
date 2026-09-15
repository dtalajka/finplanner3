using FinPlanner.Api.Domain;
using FinPlanner.Api.Reporting;
using Xunit;

namespace FinPlanner.Api.Tests;

public class PlanComplianceCalculatorTests
{
    private static Category MakeCategory(long id, string name, CategoryType type) =>
        new() { Id = id, FamilyId = 1, Name = name, Type = type, Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static PlannedTransaction MakePlanned(long id, DateOnly date, decimal amount, long? categoryId, bool cancelled = false, long? recurringRuleId = null, long? goalId = null) =>
        new() { Id = id, FamilyId = 1, PlanId = 1, PlannedDate = date, Amount = amount, FromAccountId = 10, ToAccountId = null, CategoryId = categoryId, Cancelled = cancelled, RecurringRuleId = recurringRuleId, GoalId = goalId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static ActualTransaction MakeActual(long id, DateOnly date, decimal amount, long? categoryId, long? plannedTransactionId = null, long? goalId = null) =>
        new() { Id = id, FamilyId = 1, ActualDate = date, Amount = amount, FromAccountId = 10, ToAccountId = null, CategoryId = categoryId, PlannedTransactionId = plannedTransactionId, GoalId = goalId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static RecurringRule MakeRule(long id, decimal amount, long? categoryId, DateOnly startDate, short dayOfMonth = 1) =>
        new() { Id = id, FamilyId = 1, PlanId = 1, Name = "Rule", FromAccountId = 10, ToAccountId = null, CategoryId = categoryId, Amount = amount, Frequency = RecurrenceFrequency.Monthly, DayOfMonth = dayOfMonth, StartDate = startDate, Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    [Fact]
    public void MatchedActualExactlyEqualsPlanned_IsOnPlan()
    {
        var category = MakeCategory(1, "Groceries", CategoryType.Expense);
        var planned = MakePlanned(1, new DateOnly(2026, 7, 10), 1000m, 1);
        var actual = MakeActual(1, new DateOnly(2026, 7, 10), 1000m, 1, plannedTransactionId: 1);

        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [category], [actual], [planned], [], []);

        var cell = Assert.Single(result.Periods.Single().Categories);
        Assert.Equal(0m, cell.Variance);
        Assert.Equal("ON_PLAN", cell.Favorability);
        Assert.Equal(1, cell.ActualMatchedCount);
        Assert.Equal(0, cell.ActualUnmatchedCount);
    }

    [Fact]
    public void PartialCompliance_ExpenseOverspend_IsUnfavorable()
    {
        var category = MakeCategory(1, "Groceries", CategoryType.Expense);
        var planned = MakePlanned(1, new DateOnly(2026, 7, 10), 1000m, 1);
        var actual = MakeActual(1, new DateOnly(2026, 7, 10), 1100m, 1, plannedTransactionId: 1);

        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [category], [actual], [planned], [], []);

        var cell = Assert.Single(result.Periods.Single().Categories);
        Assert.Equal(100m, cell.Variance);
        Assert.Equal(10m, cell.VariancePercent);
        Assert.Equal("UNFAVORABLE", cell.Favorability);
    }

    [Fact]
    public void PartialCompliance_IncomeOverearn_IsFavorable()
    {
        var category = MakeCategory(1, "Salary", CategoryType.Income);
        var planned = MakePlanned(1, new DateOnly(2026, 7, 1), 3000m, 1);
        var actual = MakeActual(1, new DateOnly(2026, 7, 1), 3200m, 1, plannedTransactionId: 1);

        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [category], [actual], [planned], [], []);

        var cell = Assert.Single(result.Periods.Single().Categories);
        Assert.Equal(200m, cell.Variance);
        Assert.Equal("FAVORABLE", cell.Favorability);
    }

    [Fact]
    public void UnplannedActual_HasZeroPlannedAmount_AndIsUnfavorableExpense()
    {
        var category = MakeCategory(1, "Car repair", CategoryType.Expense);
        var actual = MakeActual(1, new DateOnly(2026, 7, 15), 420m, 1);

        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [category], [actual], [], [], []);

        var cell = Assert.Single(result.Periods.Single().Categories);
        Assert.Equal(0m, cell.PlannedAmount);
        Assert.Equal(420m, cell.ActualAmount);
        Assert.Equal(420m, cell.Variance);
        Assert.Equal("UNFAVORABLE", cell.Favorability);
        Assert.Equal(0, cell.ActualMatchedCount);
        Assert.Equal(1, cell.ActualUnmatchedCount);
        Assert.Null(cell.VariancePercent);
    }

    [Fact]
    public void PlannedWithoutActual_ClosedMonth_ShowsShortfall_NoOverdueRelocation()
    {
        var category = MakeCategory(1, "Car maintenance", CategoryType.Expense);
        var planned = MakePlanned(1, new DateOnly(2026, 7, 15), 50m, 1);

        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [category], [], [planned], [], []);

        var cell = Assert.Single(result.Periods.Single().Categories);
        Assert.Equal(50m, cell.PlannedAmount);
        Assert.Equal(0m, cell.ActualAmount);
        Assert.Equal(-50m, cell.Variance);
        Assert.Equal("FAVORABLE", cell.Favorability); // naive convention — see Part K §7/§20 limitation
        Assert.Equal("CLOSED", result.Periods.Single().Status);
    }

    [Fact]
    public void RecurringOccurrence_Unshadowed_CountsOnceViaExpansion()
    {
        var category = MakeCategory(1, "Rent", CategoryType.Expense);
        var rule = MakeRule(1, 700m, 1, new DateOnly(2026, 1, 1));

        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [category], [], [], [rule], []);

        var cell = Assert.Single(result.Periods.Single().Categories);
        Assert.Equal(700m, cell.PlannedAmount);
        Assert.Equal(1, cell.PlannedRecurringCount);
        Assert.Equal(0, cell.PlannedRealCount);
    }

    [Fact]
    public void RecurringOccurrence_ShadowedByRealRow_DoesNotDoubleCount()
    {
        var category = MakeCategory(1, "Rent", CategoryType.Expense);
        var rule = MakeRule(1, 700m, 1, new DateOnly(2026, 1, 1));
        var real = MakePlanned(1, new DateOnly(2026, 7, 1), 700m, 1, recurringRuleId: 1);

        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [category], [], [real], [rule], []);

        var cell = Assert.Single(result.Periods.Single().Categories);
        Assert.Equal(700m, cell.PlannedAmount); // not 1400
        Assert.Equal(1, cell.PlannedRealCount);
        Assert.Equal(0, cell.PlannedRecurringCount);
    }

    [Fact]
    public void RecurringOccurrence_ShadowedByRealRow_ExplicitRecategorizationWins()
    {
        var rentCategory = MakeCategory(1, "Rent", CategoryType.Expense);
        var repairCategory = MakeCategory(2, "Repairs", CategoryType.Expense);
        var rule = MakeRule(1, 700m, 1, new DateOnly(2026, 1, 1));
        var real = MakePlanned(1, new DateOnly(2026, 7, 1), 700m, 2, recurringRuleId: 1); // re-tagged to Repairs

        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [rentCategory, repairCategory], [], [real], [rule], []);

        Assert.DoesNotContain(result.Periods.Single().Categories, item => item.CategoryId == 1);
        var repairCell = result.Periods.Single().Categories.Single(item => item.CategoryId == 2);
        Assert.Equal(700m, repairCell.PlannedAmount);
    }

    [Fact]
    public void RecurringOccurrence_ShadowedByCancelledRealRow_ContributesNothing()
    {
        var category = MakeCategory(1, "Rent", CategoryType.Expense);
        var rule = MakeRule(1, 700m, 1, new DateOnly(2026, 1, 1));
        var real = MakePlanned(1, new DateOnly(2026, 7, 1), 700m, 1, cancelled: true, recurringRuleId: 1);

        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [category], [], [real], [rule], []);

        Assert.Empty(result.Periods.Single().Categories);
    }

    [Fact]
    public void CancelledButMatchedPlannedRow_ActualStillAttributedToItsPeriod_PlannedExcluded()
    {
        var category = MakeCategory(1, "Rent", CategoryType.Expense);
        var planned = MakePlanned(1, new DateOnly(2026, 7, 1), 700m, 1, cancelled: true);
        var actual = MakeActual(1, new DateOnly(2026, 7, 2), 700m, 1, plannedTransactionId: 1);

        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [category], [actual], [planned], [], []);

        var cell = Assert.Single(result.Periods.Single().Categories);
        Assert.Equal(0m, cell.PlannedAmount);
        Assert.Equal(700m, cell.ActualAmount);
        Assert.Equal(1, cell.ActualMatchedCount);
    }

    [Fact]
    public void Transfer_NeverAppearsInAnyCategory()
    {
        var planned = MakePlanned(1, new DateOnly(2026, 7, 1), 500m, categoryId: null);
        var actual = MakeActual(1, new DateOnly(2026, 7, 1), 500m, categoryId: null);

        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [], [actual], [planned], [], []);

        Assert.Empty(result.Periods.Single().Categories);
    }

    [Fact]
    public void TimingShift_QueryingPlannedMonthOnly_AttributesPairToPlannedPeriod()
    {
        var category = MakeCategory(1, "Mortgage", CategoryType.Expense);
        var planned = MakePlanned(1, new DateOnly(2026, 7, 31), 1200m, 1);
        var actual = MakeActual(1, new DateOnly(2026, 8, 2), 1200m, 1, plannedTransactionId: 1);

        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [category], [actual], [planned], [], []);

        var period = Assert.Single(result.Periods);
        var cell = Assert.Single(period.Categories);
        Assert.Equal(1200m, cell.PlannedAmount);
        Assert.Equal(1200m, cell.ActualAmount);
        Assert.Equal(0m, cell.Variance);
        Assert.Equal(2, cell.AverageTimingVarianceDays);
    }

    [Fact]
    public void TimingShift_QueryingActualMonthOnly_FallsBackToActualOwnDate()
    {
        var category = MakeCategory(1, "Mortgage", CategoryType.Expense);
        // Planned row (31.7.) is NOT loaded because the query window is August-only — mirrors what the
        // controller would load for a from=to=2026-08 request.
        var actual = MakeActual(1, new DateOnly(2026, 8, 2), 1200m, 1, plannedTransactionId: 1);

        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31),
            [category], [actual], [], [], []);

        var cell = Assert.Single(result.Periods.Single().Categories);
        Assert.Equal(0m, cell.PlannedAmount);
        Assert.Equal(1200m, cell.ActualAmount);
        Assert.Equal(0, cell.ActualMatchedCount); // planned row not resolvable in this window -> falls back, counted as "unmatched" for this window's purposes
        Assert.Equal(1, cell.ActualUnmatchedCount);
    }

    [Fact]
    public void TimingShift_QueryingBothMonths_AppearsOnlyOnceInPlannedMonth()
    {
        var category = MakeCategory(1, "Mortgage", CategoryType.Expense);
        var planned = MakePlanned(1, new DateOnly(2026, 7, 31), 1200m, 1);
        var actual = MakeActual(1, new DateOnly(2026, 8, 2), 1200m, 1, plannedTransactionId: 1);

        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 31),
            [category], [actual], [planned], [], []);

        Assert.Equal(2, result.Periods.Count);
        var july = result.Periods.Single(period => period.PeriodStart.Month == 7);
        var august = result.Periods.Single(period => period.PeriodStart.Month == 8);
        Assert.Single(july.Categories);
        Assert.Empty(august.Categories);
    }

    [Fact]
    public void PeriodStatus_ClosedInProgressFuture()
    {
        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 8, 1), new DateOnly(2026, 10, 31),
            [], [], [], [], []);

        Assert.Equal("CLOSED", result.Periods[0].Status);
        Assert.Equal("IN_PROGRESS", result.Periods[1].Status);
        Assert.Equal("FUTURE", result.Periods[2].Status);
    }

    [Fact]
    public void GoalContribution_TrackedSeparatelyFromCategory_ExceedingPlanIsFavorable()
    {
        var category = MakeCategory(1, "Savings", CategoryType.Expense);
        var goal = new Goal { Id = 1, FamilyId = 1, PlanId = 1, Name = "Vacation", TargetAmount = 2000m, TargetDate = new DateOnly(2027, 1, 1), Priority = GoalPriority.Soft, Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var planned = MakePlanned(1, new DateOnly(2026, 7, 1), 200m, 1, goalId: 1);
        var actual = MakeActual(1, new DateOnly(2026, 7, 1), 250m, 1, plannedTransactionId: 1, goalId: 1);

        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [category], [actual], [planned], [], [goal]);

        var goalCell = Assert.Single(result.Periods.Single().Goals);
        Assert.Equal(200m, goalCell.PlannedContribution);
        Assert.Equal(250m, goalCell.ActualContribution);
        Assert.Equal(50m, goalCell.Variance);
        Assert.Equal("FAVORABLE", goalCell.Favorability); // more contribution than planned = progress, not overspend
    }

    [Fact]
    public void CategoriesOnPlanRatio_IsUnweightedByAmount()
    {
        var big = MakeCategory(1, "Mortgage", CategoryType.Expense);
        var small = MakeCategory(2, "Netflix", CategoryType.Expense);
        var plannedBig = MakePlanned(1, new DateOnly(2026, 7, 1), 1200m, 1);
        var actualBig = MakeActual(1, new DateOnly(2026, 7, 1), 1200m, 1, plannedTransactionId: 1); // on plan
        var plannedSmall = MakePlanned(2, new DateOnly(2026, 7, 1), 10m, 2);
        var actualSmall = MakeActual(2, new DateOnly(2026, 7, 1), 20m, 2, plannedTransactionId: 2); // unfavorable

        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [big, small], [actualBig, actualSmall], [plannedBig, plannedSmall], [], []);

        Assert.Equal(0.5m, result.Periods.Single().Totals.CategoriesOnPlanRatio);
    }

    [Fact]
    public void EmptyRange_NoAccountsOrTransactions_DoesNotThrow()
    {
        var result = PlanComplianceCalculator.Build(new DateOnly(2026, 9, 15), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [], [], [], [], []);

        var period = Assert.Single(result.Periods);
        Assert.Empty(period.Categories);
        Assert.Null(period.Totals.CategoriesOnPlanRatio);
    }
}
