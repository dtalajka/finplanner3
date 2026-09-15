using FinPlanner.Api.Domain;
using FinPlanner.Api.Forecasting;
using Xunit;

namespace FinPlanner.Api.Tests;

public class ForecastCalculatorTests
{
    private static Account NewAccount(long id, string name = "Joint", bool active = true) =>
        new() { Id = id, FamilyId = 1, Name = name, Currency = "EUR", OpeningBalance = 0, Active = active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static ActualTransaction Actual(long id, DateOnly date, decimal amount, long? from, long? to, long? goalId = null, long? plannedTransactionId = null) =>
        new() { Id = id, FamilyId = 1, ActualDate = date, Amount = amount, FromAccountId = from, ToAccountId = to, GoalId = goalId, PlannedTransactionId = plannedTransactionId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static PlannedTransaction Planned(long id, DateOnly date, decimal amount, long? from, long? to, bool cancelled = false, long? recurringRuleId = null, long? goalId = null) =>
        new() { Id = id, FamilyId = 1, PlanId = 1, PlannedDate = date, Amount = amount, FromAccountId = from, ToAccountId = to, Cancelled = cancelled, RecurringRuleId = recurringRuleId, GoalId = goalId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static RecurringRule Recurring(long id, DateOnly startDate, decimal amount, long? from, long? to, bool active = true) =>
        new() { Id = id, FamilyId = 1, PlanId = 1, Name = "Rule", Amount = amount, FromAccountId = from, ToAccountId = to, Frequency = RecurrenceFrequency.Monthly, StartDate = startDate, Active = active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static Goal NewGoal(long id, long? recurringRuleId = null, bool active = true) =>
        new() { Id = id, FamilyId = 1, PlanId = 1, Name = "Goal", TargetAmount = 1000, TargetDate = new DateOnly(2027, 1, 1), Priority = GoalPriority.Soft, RecurringRuleId = recurringRuleId, Active = active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    [Fact]
    public void PartE_WorkedExample_MinimumIsMidWalk_NotTheFinalBalance()
    {
        // Reproduces the worked example from the approved Part E §1 financial-semantics spec.
        var account = NewAccount(1, "Joint");
        var t0 = new DateOnly(2026, 9, 14);
        var actuals = new[]
        {
            Actual(1, new DateOnly(2026, 9, 1), 5000m, null, 1),
            Actual(2, new DateOnly(2026, 9, 5), 1200m, 1, null),
            Actual(3, new DateOnly(2026, 9, 10), 600m, 1, null),
        };
        var planned = new[]
        {
            Planned(1, new DateOnly(2026, 9, 20), 30m, 1, null),
            Planned(2, new DateOnly(2026, 10, 1), 5000m, null, 1),
            Planned(3, new DateOnly(2026, 10, 5), 1200m, 1, null),
            Planned(4, new DateOnly(2026, 10, 10), 600m, 1, null),
            Planned(5, new DateOnly(2026, 10, 12), 960m, 1, null),
        };

        var result = ForecastCalculator.Build(t0, new DateOnly(2026, 10, 14), [account], actuals, planned, [], []);

        Assert.Equal(3200m, result.Accounts[0].CurrentBalance);
        Assert.Equal(3170m, result.MinimumFutureBalance);
        Assert.Equal(new DateOnly(2026, 9, 20), result.MinimumFutureBalanceDate);
        Assert.Equal(5410m, result.Trajectory[^1].TotalBalance);
    }

    [Fact]
    public void MatchedPlanned_IsExcluded_OnlyTheActualCounts()
    {
        var account = NewAccount(1);
        var planned = new[] { Planned(1, new DateOnly(2026, 10, 5), 1200m, 1, null) };
        var actuals = new[] { Actual(1, new DateOnly(2026, 10, 5), 1200m, 1, null, plannedTransactionId: 1) };
        var t0 = new DateOnly(2026, 10, 14); // after both dates: the actual folds into balance_T0, isn't a future event

        var result = ForecastCalculator.Build(t0, new DateOnly(2026, 11, 1), [account], actuals, planned, [], []);

        Assert.Empty(result.Events); // matched planned excluded; the actual already happened and is part of balance_T0
        Assert.Equal(-1200m, result.Accounts[0].CurrentBalance);
    }

    [Fact]
    public void FutureDatedActual_AppearsAsItsOwnForecastEvent()
    {
        var account = NewAccount(1);
        var actuals = new[] { Actual(1, new DateOnly(2026, 10, 5), 500m, null, 1) }; // backdated-ahead entry
        var t0 = new DateOnly(2026, 9, 14);

        var result = ForecastCalculator.Build(t0, new DateOnly(2026, 11, 1), [account], actuals, [], [], []);

        var evt = Assert.Single(result.Events);
        Assert.Equal("ACTUAL", evt.Source);
        Assert.Equal("UNPLANNED", evt.Status);
        Assert.Equal(new DateOnly(2026, 10, 5), evt.Date);
    }

    [Fact]
    public void CancelledPlanned_IsExcluded()
    {
        var account = NewAccount(1);
        var planned = new[] { Planned(1, new DateOnly(2026, 10, 5), 1200m, 1, null, cancelled: true) };

        var result = ForecastCalculator.Build(new DateOnly(2026, 9, 14), new DateOnly(2026, 11, 1), [account], [], planned, [], []);

        Assert.Empty(result.Events);
    }

    [Fact]
    public void OverduePlanned_IsRelocatedToT0()
    {
        var account = NewAccount(1);
        var t0 = new DateOnly(2026, 9, 14);
        var planned = new[] { Planned(1, new DateOnly(2026, 9, 5), 1200m, 1, null) };

        var result = ForecastCalculator.Build(t0, new DateOnly(2026, 11, 1), [account], [], planned, [], []);

        var evt = Assert.Single(result.Events);
        Assert.Equal(t0, evt.Date);
        Assert.Equal("OVERDUE", evt.Status);
    }

    [Fact]
    public void VirtualOccurrence_IsShadowedByRealPlannedRow_ForSameRuleAndDate()
    {
        var account = NewAccount(1);
        var rule = Recurring(1, new DateOnly(2026, 9, 1), 3500m, null, 1);
        var t0 = new DateOnly(2026, 9, 14);
        // A real row exists for the October occurrence, with an edited amount.
        var planned = new[] { Planned(1, new DateOnly(2026, 10, 1), 3600m, null, 1, recurringRuleId: 1) };

        var result = ForecastCalculator.Build(t0, new DateOnly(2026, 11, 1), [account], [], planned, [rule], []);

        Assert.Equal(2, result.Events.Count); // October (real, edited) + November (virtual) — no duplicate for October
        Assert.Contains(result.Events, e => e.Date == new DateOnly(2026, 10, 1) && e.Amount == 3600m && e.Source == "PLANNED");
        Assert.Contains(result.Events, e => e.Date == new DateOnly(2026, 11, 1) && e.Amount == 3500m && e.Source == "RECURRING");
    }

    [Fact]
    public void VirtualOccurrences_AreNeverGeneratedBeforeT0()
    {
        var account = NewAccount(1);
        var rule = Recurring(1, new DateOnly(2020, 1, 1), 100m, 1, null);
        var t0 = new DateOnly(2026, 9, 14);

        var result = ForecastCalculator.Build(t0, new DateOnly(2026, 10, 31), [account], [], [], [rule], []);

        Assert.NotEmpty(result.Events);
        Assert.All(result.Events, e => Assert.True(e.Date >= t0));
        Assert.All(result.Events, e => Assert.Equal("PLANNED", e.Status)); // never OVERDUE
    }

    [Fact]
    public void InactiveRecurringRule_GeneratesNoOccurrences()
    {
        var account = NewAccount(1);
        var rule = Recurring(1, new DateOnly(2026, 1, 1), 100m, 1, null, active: false);

        var result = ForecastCalculator.Build(new DateOnly(2026, 9, 14), new DateOnly(2026, 10, 31), [account], [], [], [rule], []);

        Assert.Empty(result.Events);
    }

    [Fact]
    public void Transfer_LeavesTotalBalanceUnchanged_ButMovesPerAccountBalances()
    {
        var accountA = NewAccount(1, "A");
        var accountB = NewAccount(2, "B");
        var planned = new[] { Planned(1, new DateOnly(2026, 9, 20), 500m, 1, 2) };

        var result = ForecastCalculator.Build(new DateOnly(2026, 9, 14), new DateOnly(2026, 10, 1), [accountA, accountB], [], planned, [], []);

        var point = Assert.Single(result.Trajectory);
        Assert.Equal(0m, point.TotalBalance); // both accounts start at 0; -500 + 500 = 0
        Assert.Equal(-500m, point.AccountBalances[1]);
        Assert.Equal(500m, point.AccountBalances[2]);
    }

    [Fact]
    public void InactiveAccount_IsExcludedEntirely_NotJustFromTrajectory()
    {
        // Only the active account is passed in, per the documented contract.
        var activeAccount = NewAccount(1, "Joint", active: true);
        var actuals = new[] { Actual(1, new DateOnly(2026, 9, 1), 100m, null, 1) };

        var result = ForecastCalculator.Build(new DateOnly(2026, 9, 14), new DateOnly(2026, 10, 1), [activeAccount], actuals, [], [], []);

        Assert.Single(result.Accounts);
        Assert.Equal(100m, result.Accounts[0].CurrentBalance);
    }

    [Fact]
    public void GoalId_IsAutoTaggedOntoVirtualOccurrences_ViaGoalRecurringRuleId()
    {
        var account = NewAccount(1);
        var rule = Recurring(1, new DateOnly(2026, 9, 1), 200m, 1, null);
        var goal = NewGoal(10, recurringRuleId: 1);

        var result = ForecastCalculator.Build(new DateOnly(2026, 9, 14), new DateOnly(2026, 10, 31), [account], [], [], [rule], [goal]);

        Assert.NotEmpty(result.Events);
        Assert.All(result.Events, e => Assert.Equal(10L, e.GoalId));
    }

    [Fact]
    public void ExplicitGoalIdOnRealRow_OverridesTheRuleImpliedTag()
    {
        var account = NewAccount(1);
        var rule = Recurring(1, new DateOnly(2026, 9, 1), 200m, 1, null);
        var goal = NewGoal(10, recurringRuleId: 1);
        // Real row for October explicitly tagged to a different goal (goalId 99).
        var planned = new[] { Planned(1, new DateOnly(2026, 10, 1), 200m, 1, null, recurringRuleId: 1, goalId: 99) };

        var result = ForecastCalculator.Build(new DateOnly(2026, 9, 14), new DateOnly(2026, 10, 31), [account], [], planned, [rule], [goal]);

        var octoberEvent = Assert.Single(result.Events, e => e.Date == new DateOnly(2026, 10, 1));
        Assert.Equal(99L, octoberEvent.GoalId);
    }

    [Fact]
    public void TwoGoalsOnSameRecurringRule_LowestIdWins()
    {
        var account = NewAccount(1);
        var rule = Recurring(1, new DateOnly(2026, 9, 1), 200m, 1, null);
        var goalLow = NewGoal(5, recurringRuleId: 1);
        var goalHigh = NewGoal(7, recurringRuleId: 1);

        var result = ForecastCalculator.Build(new DateOnly(2026, 9, 14), new DateOnly(2026, 10, 31), [account], [], [], [rule], [goalHigh, goalLow]);

        Assert.NotEmpty(result.Events);
        Assert.All(result.Events, e => Assert.Equal(5L, e.GoalId));
    }

    [Fact]
    public void NoData_ProducesFlatTrajectoryAtBalanceT0()
    {
        var account = NewAccount(1);
        var actuals = new[] { Actual(1, new DateOnly(2026, 9, 1), 1000m, null, 1) };

        var result = ForecastCalculator.Build(new DateOnly(2026, 9, 14), new DateOnly(2026, 10, 14), [account], actuals, [], [], []);

        Assert.Empty(result.Events);
        Assert.Empty(result.Trajectory);
        Assert.Equal(1000m, result.MinimumFutureBalance);
        Assert.Equal(new DateOnly(2026, 9, 14), result.MinimumFutureBalanceDate);
        Assert.False(result.NegativeBalanceRisk);
    }
}
