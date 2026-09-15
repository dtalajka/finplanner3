using FinPlanner.Api.Domain;

namespace FinPlanner.Api.Forecasting;

public sealed record GoalFeasibility(long GoalId, string Name, GoalPriority Priority, string Status);

public sealed record AvailableToSpendResult(
    DateOnly AsOf, DateOnly HorizonEnd, decimal MinimumReserve, decimal ForecastMinBalance, DateOnly ForecastMinBalanceDate,
    decimal AvailableToSpend, decimal ReserveShortfall, string PlanStatus, IReadOnlyList<GoalFeasibility> Goals);

/// <summary>
/// Layered on top of <see cref="ForecastCalculator"/> (never modifies or duplicates it — Part I decision #2):
/// HARD goals are always included; SOFT goals are accepted one at a time, nearest target date first, by
/// re-running <see cref="ForecastCalculator.Build"/> on input lists with that goal's tagged events filtered
/// in/out and checking whether the resulting minimum still clears <paramref name="minimumReserve"/>.
/// </summary>
public static class AvailableToSpendCalculator
{
    public static AvailableToSpendResult Build(
        DateOnly asOf,
        DateOnly horizonEnd,
        decimal minimumReserve,
        IReadOnlyList<Account> accounts,
        IReadOnlyList<ActualTransaction> actualTransactions,
        IReadOnlyList<PlannedTransaction> plannedTransactions,
        IReadOnlyList<RecurringRule> recurringRules,
        IReadOnlyList<Goal> goals)
    {
        var activeGoals = goals.Where(goal => goal.Active).ToList();
        var softGoalsInOrder = activeGoals.Where(goal => goal.Priority == GoalPriority.Soft)
            .OrderBy(goal => goal.TargetDate).ThenBy(goal => goal.Id).ToList();

        var excludedGoalIds = softGoalsInOrder.Select(goal => goal.Id).ToHashSet();
        var baseline = BuildFiltered(asOf, horizonEnd, accounts, actualTransactions, plannedTransactions, recurringRules, activeGoals, excludedGoalIds);

        var goalResults = new List<GoalFeasibility>();
        foreach (var goal in activeGoals.Where(goal => goal.Priority == GoalPriority.Hard))
            goalResults.Add(new GoalFeasibility(goal.Id, goal.Name, goal.Priority, "FUNDED"));

        // If hard goals alone already breach the reserve, every soft goal's own feasibility check would
        // necessarily fail too (goal contributions only ever subtract from total_balance or net to zero —
        // see Part E §5/Part I §7 — so adding one can never raise the minimum back above the reserve).
        // Skipping the trial loop here is a performance optimization, not a semantic difference.
        var hardGoalsAloneAreInfeasible = baseline.MinimumFutureBalance < minimumReserve;

        foreach (var goal in softGoalsInOrder)
        {
            if (hardGoalsAloneAreInfeasible)
            {
                goalResults.Add(new GoalFeasibility(goal.Id, goal.Name, goal.Priority, "AT_RISK"));
                continue;
            }

            excludedGoalIds.Remove(goal.Id);
            var trial = BuildFiltered(asOf, horizonEnd, accounts, actualTransactions, plannedTransactions, recurringRules, activeGoals, excludedGoalIds);
            if (trial.MinimumFutureBalance >= minimumReserve)
            {
                baseline = trial;
                goalResults.Add(new GoalFeasibility(goal.Id, goal.Name, goal.Priority, "FUNDED"));
            }
            else
            {
                excludedGoalIds.Add(goal.Id);
                goalResults.Add(new GoalFeasibility(goal.Id, goal.Name, goal.Priority, "AT_RISK"));
            }
        }

        var reserveShortfall = Math.Max(0m, minimumReserve - baseline.MinimumFutureBalance);
        var availableToSpend = Math.Max(0m, baseline.MinimumFutureBalance - minimumReserve);
        var planStatus = reserveShortfall > 0 ? "AT_RISK" : "ON_TRACK";

        return new AvailableToSpendResult(asOf, horizonEnd, minimumReserve, baseline.MinimumFutureBalance, baseline.MinimumFutureBalanceDate,
            availableToSpend, reserveShortfall, planStatus, goalResults);
    }

    private static ForecastResult BuildFiltered(
        DateOnly asOf, DateOnly horizonEnd, IReadOnlyList<Account> accounts, IReadOnlyList<ActualTransaction> actualTransactions,
        IReadOnlyList<PlannedTransaction> plannedTransactions, IReadOnlyList<RecurringRule> recurringRules, IReadOnlyList<Goal> activeGoals, HashSet<long> excludedGoalIds)
    {
        var excludedRuleIds = activeGoals.Where(goal => excludedGoalIds.Contains(goal.Id) && goal.RecurringRuleId is not null)
            .Select(goal => goal.RecurringRuleId!.Value).ToHashSet();

        // A real row's own GoalId always wins over what its recurring rule would imply (Phase 4 behavior,
        // unchanged) — so filtering by the row's own GoalId here, not its RecurringRuleId, is deliberate.
        var filteredPlanned = plannedTransactions.Where(item => item.GoalId is null || !excludedGoalIds.Contains(item.GoalId.Value)).ToList();
        var filteredRules = recurringRules.Where(rule => !excludedRuleIds.Contains(rule.Id)).ToList();

        return ForecastCalculator.Build(asOf, horizonEnd, accounts, actualTransactions, filteredPlanned, filteredRules, activeGoals);
    }
}
