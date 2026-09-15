using FinPlanner.Api.Domain;
using FinPlanner.Api.Forecasting;
using Xunit;

namespace FinPlanner.Api.Tests;

public class AvailableToSpendCalculatorTests
{
    private static Account NewAccount(long id, decimal openingBalance, string name = "Joint") =>
        new() { Id = id, FamilyId = 1, Name = name, Currency = "EUR", OpeningBalance = openingBalance, Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static PlannedTransaction Planned(long id, DateOnly date, decimal amount, long? from, long? to, long? goalId = null) =>
        new() { Id = id, FamilyId = 1, PlanId = 1, PlannedDate = date, Amount = amount, FromAccountId = from, ToAccountId = to, GoalId = goalId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static RecurringRule Recurring(long id, DateOnly startDate, decimal amount, long? from, long? to) =>
        new() { Id = id, FamilyId = 1, PlanId = 1, Name = "Rule", Amount = amount, FromAccountId = from, ToAccountId = to, Frequency = RecurrenceFrequency.Monthly, StartDate = startDate, Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static Goal NewGoal(long id, string name, GoalPriority priority, DateOnly targetDate) =>
        new() { Id = id, FamilyId = 1, PlanId = 1, Name = name, TargetAmount = 1000, TargetDate = targetDate, Priority = priority, Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    [Fact]
    public void HardAndFeasibleSoftGoal_BothFunded_MatchesPartEWorkedExample()
    {
        // Reproduces the Part E §2 worked example verbatim: balance_T0 = 3200, min = 3170 on 09-20,
        // reserve = 500 -> ATS = 2670, both goals FUNDED, plan ON_TRACK.
        var account = NewAccount(1, 3200m);
        var t0 = new DateOnly(2026, 9, 14);
        var horizonEnd = new DateOnly(2026, 12, 20);
        var hardGoal = NewGoal(1, "Life insurance", GoalPriority.Hard, new DateOnly(2026, 12, 15));
        var softGoal = NewGoal(2, "Vacation", GoalPriority.Soft, new DateOnly(2027, 4, 1));

        var planned = new[]
        {
            Planned(1, new DateOnly(2026, 9, 20), 30m, 1, null),
            Planned(2, new DateOnly(2026, 10, 1), 5000m, null, 1),
            Planned(3, new DateOnly(2026, 10, 1), 200m, 1, null, goalId: softGoal.Id),
            Planned(4, new DateOnly(2026, 10, 5), 1200m, 1, null),
            Planned(5, new DateOnly(2026, 10, 10), 600m, 1, null),
            Planned(6, new DateOnly(2026, 11, 1), 5000m, null, 1),
            Planned(7, new DateOnly(2026, 11, 1), 200m, 1, null, goalId: softGoal.Id),
            Planned(8, new DateOnly(2026, 11, 5), 1200m, 1, null),
            Planned(9, new DateOnly(2026, 11, 10), 600m, 1, null),
            Planned(10, new DateOnly(2026, 12, 1), 5000m, null, 1),
            Planned(11, new DateOnly(2026, 12, 1), 200m, 1, null, goalId: softGoal.Id),
            Planned(12, new DateOnly(2026, 12, 5), 1200m, 1, null),
            Planned(13, new DateOnly(2026, 12, 10), 600m, 1, null),
            Planned(14, new DateOnly(2026, 12, 15), 960m, 1, null, goalId: hardGoal.Id),
        };

        var result = AvailableToSpendCalculator.Build(t0, horizonEnd, 500m, [account], [], planned, [], [hardGoal, softGoal]);

        Assert.Equal(3170m, result.ForecastMinBalance);
        Assert.Equal(2670m, result.AvailableToSpend);
        Assert.Equal(0m, result.ReserveShortfall);
        Assert.Equal("ON_TRACK", result.PlanStatus);
        Assert.Equal("FUNDED", result.Goals.Single(g => g.GoalId == hardGoal.Id).Status);
        Assert.Equal("FUNDED", result.Goals.Single(g => g.GoalId == softGoal.Id).Status);
    }

    [Fact]
    public void InfeasibleSoftGoal_IsExcludedAndFlaggedAtRisk_MatchesPartERejectedVariant()
    {
        // Same family, but vacation contributions land on the 25th (not alongside salary) and reserve = 3000:
        // including vacation would dip to 2870 on 09-25 (< 3000) -> rejected; ATS recomputed without it = 170.
        var account = NewAccount(1, 3200m);
        var t0 = new DateOnly(2026, 9, 14);
        var horizonEnd = new DateOnly(2026, 12, 20);
        var softGoal = NewGoal(2, "Vacation", GoalPriority.Soft, new DateOnly(2027, 4, 1));

        var planned = new[]
        {
            Planned(1, new DateOnly(2026, 9, 20), 30m, 1, null),
            Planned(2, new DateOnly(2026, 9, 25), 300m, 1, null, goalId: softGoal.Id),
            Planned(3, new DateOnly(2026, 10, 1), 5000m, null, 1),
        };

        var result = AvailableToSpendCalculator.Build(t0, horizonEnd, 3000m, [account], [], planned, [], [softGoal]);

        Assert.Equal(3170m, result.ForecastMinBalance);
        Assert.Equal(170m, result.AvailableToSpend);
        Assert.Equal("AT_RISK", result.Goals.Single(g => g.GoalId == softGoal.Id).Status);
        Assert.Equal("ON_TRACK", result.PlanStatus); // hard goals (none here) are fine; only the soft goal was sacrificed
    }

    [Fact]
    public void MultipleHardGoalsBreachingReserve_NoArbitration_PlanAtRiskAtsZero()
    {
        var account = NewAccount(1, 1000m);
        var t0 = new DateOnly(2026, 9, 14);
        var horizonEnd = new DateOnly(2026, 10, 14);
        var hardGoalA = NewGoal(1, "Hard A", GoalPriority.Hard, new DateOnly(2026, 9, 20));
        var hardGoalB = NewGoal(2, "Hard B", GoalPriority.Hard, new DateOnly(2026, 9, 25));
        var planned = new[]
        {
            Planned(1, new DateOnly(2026, 9, 20), 700m, 1, null, goalId: hardGoalA.Id),
            Planned(2, new DateOnly(2026, 9, 25), 700m, 1, null, goalId: hardGoalB.Id),
        };

        var result = AvailableToSpendCalculator.Build(t0, horizonEnd, 0m, [account], [], planned, [], [hardGoalA, hardGoalB]);

        Assert.Equal(-400m, result.ForecastMinBalance); // both hard events applied: 1000 - 700 - 700 = -400
        Assert.Equal(0m, result.AvailableToSpend);
        Assert.Equal(400m, result.ReserveShortfall);
        Assert.Equal("AT_RISK", result.PlanStatus);
        Assert.All(result.Goals, g => Assert.Equal("FUNDED", g.Status)); // neither hard goal is ever excluded
    }

    [Fact]
    public void HardGoalsAloneInfeasible_AllSoftGoalsAutoRejected()
    {
        var account = NewAccount(1, 100m);
        var t0 = new DateOnly(2026, 9, 14);
        var horizonEnd = new DateOnly(2026, 10, 14);
        var hardGoal = NewGoal(1, "Hard", GoalPriority.Hard, new DateOnly(2026, 9, 20));
        var softA = NewGoal(2, "Soft A", GoalPriority.Soft, new DateOnly(2026, 9, 21));
        var softB = NewGoal(3, "Soft B", GoalPriority.Soft, new DateOnly(2026, 9, 22));
        var planned = new[]
        {
            Planned(1, new DateOnly(2026, 9, 20), 500m, 1, null, goalId: hardGoal.Id), // already breaches reserve alone
        };

        var result = AvailableToSpendCalculator.Build(t0, horizonEnd, 0m, [account], [], planned, [], [hardGoal, softA, softB]);

        Assert.Equal("AT_RISK", result.Goals.Single(g => g.GoalId == softA.Id).Status);
        Assert.Equal("AT_RISK", result.Goals.Single(g => g.GoalId == softB.Id).Status);
    }

    [Fact]
    public void TwoSoftGoals_EarlierTargetDateWins_WhenOnlyOneFitsInRemainingHeadroom()
    {
        var account = NewAccount(1, 100m);
        var t0 = new DateOnly(2026, 9, 14);
        var horizonEnd = new DateOnly(2026, 10, 14);
        var earlier = NewGoal(1, "Earlier", GoalPriority.Soft, new DateOnly(2026, 12, 1));
        var later = NewGoal(2, "Later", GoalPriority.Soft, new DateOnly(2027, 1, 1));
        var planned = new[]
        {
            Planned(1, new DateOnly(2026, 9, 20), 80m, 1, null, goalId: earlier.Id),
            Planned(2, new DateOnly(2026, 9, 21), 80m, 1, null, goalId: later.Id),
        };

        var result = AvailableToSpendCalculator.Build(t0, horizonEnd, 0m, [account], [], planned, [], [earlier, later]);

        Assert.Equal("FUNDED", result.Goals.Single(g => g.GoalId == earlier.Id).Status);
        Assert.Equal("AT_RISK", result.Goals.Single(g => g.GoalId == later.Id).Status);
    }

    [Fact]
    public void TwoSoftGoalsSameTargetDate_LowestIdWins()
    {
        var account = NewAccount(1, 100m);
        var t0 = new DateOnly(2026, 9, 14);
        var horizonEnd = new DateOnly(2026, 10, 14);
        var sameDate = new DateOnly(2026, 12, 1);
        var lower = NewGoal(1, "Lower id", GoalPriority.Soft, sameDate);
        var higher = NewGoal(2, "Higher id", GoalPriority.Soft, sameDate);
        var planned = new[]
        {
            Planned(1, new DateOnly(2026, 9, 20), 80m, 1, null, goalId: lower.Id),
            Planned(2, new DateOnly(2026, 9, 21), 80m, 1, null, goalId: higher.Id),
        };

        var result = AvailableToSpendCalculator.Build(t0, horizonEnd, 0m, [account], [], planned, [], [higher, lower]);

        Assert.Equal("FUNDED", result.Goals.Single(g => g.GoalId == lower.Id).Status);
        Assert.Equal("AT_RISK", result.Goals.Single(g => g.GoalId == higher.Id).Status);
    }

    [Fact]
    public void GoalTaggedTransfer_HasNoEffectOnFeasibility_NetsToZero()
    {
        var accountA = NewAccount(1, 100m, "Joint");
        var accountB = NewAccount(2, 0m, "Savings");
        var t0 = new DateOnly(2026, 9, 14);
        var horizonEnd = new DateOnly(2026, 10, 14);
        var softGoal = NewGoal(1, "Sinking fund via transfer", GoalPriority.Soft, new DateOnly(2027, 1, 1));
        var planned = new[] { Planned(1, new DateOnly(2026, 9, 20), 90m, 1, 2, goalId: softGoal.Id) };

        var result = AvailableToSpendCalculator.Build(t0, horizonEnd, 50m, [accountA, accountB], [], planned, [], [softGoal]);

        Assert.Equal(100m, result.ForecastMinBalance); // transfer nets to zero: pooled total is unaffected
        Assert.Equal("FUNDED", result.Goals.Single().Status); // trivially feasible, exactly as Part I §7 predicts
    }

    [Fact]
    public void ReserveHigherThanCurrentBalance_ImmediatelyAtRisk()
    {
        var account = NewAccount(1, 100m);
        var t0 = new DateOnly(2026, 9, 14);

        var result = AvailableToSpendCalculator.Build(t0, t0.AddMonths(1), 500m, [account], [], [], [], []);

        Assert.Equal("AT_RISK", result.PlanStatus);
        Assert.Equal(0m, result.AvailableToSpend);
        Assert.Equal(400m, result.ReserveShortfall);
    }

    [Fact]
    public void LongerHorizon_AvailableToSpend_IsMonotonicallyNonIncreasing()
    {
        var account = NewAccount(1, 5000m);
        var t0 = new DateOnly(2026, 9, 14);
        // A large expense far out into the year should only ever lower (or not change) ATS as the horizon grows to include it.
        var planned = new[] { Planned(1, new DateOnly(2027, 6, 1), 4000m, 1, null) };

        var shortHorizon = AvailableToSpendCalculator.Build(t0, t0.AddMonths(3), 0m, [account], [], planned, [], []);
        var longHorizon = AvailableToSpendCalculator.Build(t0, t0.AddMonths(12), 0m, [account], [], planned, [], []);

        Assert.True(longHorizon.AvailableToSpend <= shortHorizon.AvailableToSpend);
        Assert.Equal(5000m, shortHorizon.AvailableToSpend); // expense is outside the 3-month window
        Assert.Equal(1000m, longHorizon.AvailableToSpend); // expense falls inside the 12-month window
    }
}
