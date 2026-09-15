using FinPlanner.Api.Domain;

namespace FinPlanner.Api.Forecasting;

public sealed record ForecastAccountBalance(long AccountId, string Name, decimal CurrentBalance);

public sealed record ForecastEvent(
    DateOnly Date, decimal Amount, long? FromAccountId, long? ToAccountId, long? CategoryId, long? GoalId,
    string TransactionType, string Source, string Status, long? PlannedTransactionId, long? RecurringRuleId, string? Description);

public sealed record ForecastTrajectoryPoint(DateOnly Date, IReadOnlyDictionary<long, decimal> AccountBalances, decimal TotalBalance);

public sealed record ForecastResult(
    DateOnly AsOf, DateOnly HorizonEnd, IReadOnlyList<ForecastAccountBalance> Accounts, IReadOnlyList<ForecastEvent> Events,
    IReadOnlyList<ForecastTrajectoryPoint> Trajectory, decimal MinimumFutureBalance, DateOnly MinimumFutureBalanceDate, bool NegativeBalanceRisk);

/// <summary>
/// Pure, deterministic forecast walk. No DbContext access and no wall-clock reads happen here —
/// "today" (<paramref name="asOf"/>) is always supplied by the caller. Only <paramref name="accounts"/>
/// passed in participate in any balance; an effect referencing an account not in that list is a no-op for
/// that side (this is how inactive accounts are excluded from the whole forecast, not just the trajectory).
/// </summary>
public static class ForecastCalculator
{
    public static ForecastResult Build(
        DateOnly asOf,
        DateOnly horizonEnd,
        IReadOnlyList<Account> accounts,
        IReadOnlyList<ActualTransaction> actualTransactions,
        IReadOnlyList<PlannedTransaction> plannedTransactions,
        IReadOnlyList<RecurringRule> recurringRules,
        IReadOnlyList<Goal> goals)
    {
        var balances = accounts.ToDictionary(account => account.Id, account => account.OpeningBalance);
        foreach (var actual in actualTransactions.Where(item => item.ActualDate <= asOf).OrderBy(item => item.ActualDate).ThenBy(item => item.Id))
            ApplyEffect(balances, actual.FromAccountId, actual.ToAccountId, actual.Amount);

        var accountsAtT0 = accounts.Select(account => new ForecastAccountBalance(account.Id, account.Name, balances[account.Id])).ToList();

        var matchedPlannedIds = actualTransactions.Where(item => item.PlannedTransactionId is not null)
            .Select(item => item.PlannedTransactionId!.Value).ToHashSet();
        var shadowedRecurringDates = plannedTransactions.Where(item => item.RecurringRuleId is not null)
            .Select(item => (item.RecurringRuleId!.Value, item.PlannedDate)).ToHashSet();
        var goalIdByRecurringRuleId = goals.Where(goal => goal.Active && goal.RecurringRuleId is not null)
            .GroupBy(goal => goal.RecurringRuleId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(goal => goal.Id).First().Id);

        var events = new List<ForecastEvent>();

        foreach (var actual in actualTransactions.Where(item => item.ActualDate > asOf))
            events.Add(new ForecastEvent(actual.ActualDate, actual.Amount, actual.FromAccountId, actual.ToAccountId, actual.CategoryId, actual.GoalId,
                TransactionType(actual.FromAccountId, actual.ToAccountId), "ACTUAL", actual.PlannedTransactionId is not null ? "MATCHED" : "UNPLANNED",
                actual.PlannedTransactionId, null, actual.Description));

        foreach (var item in plannedTransactions)
        {
            if (item.Cancelled) continue;
            if (matchedPlannedIds.Contains(item.Id)) continue;
            var isOverdue = item.PlannedDate < asOf;
            var date = isOverdue ? asOf : item.PlannedDate;
            events.Add(new ForecastEvent(date, item.Amount, item.FromAccountId, item.ToAccountId, item.CategoryId, item.GoalId,
                TransactionType(item.FromAccountId, item.ToAccountId), "PLANNED", isOverdue ? "OVERDUE" : "PLANNED",
                item.Id, item.RecurringRuleId, item.Description));
        }

        foreach (var rule in recurringRules.Where(item => item.Active))
        {
            foreach (var date in RecurringRuleExpander.Expand(rule, asOf, horizonEnd))
            {
                if (shadowedRecurringDates.Contains((rule.Id, date))) continue;
                var goalId = goalIdByRecurringRuleId.TryGetValue(rule.Id, out var id) ? id : (long?)null;
                events.Add(new ForecastEvent(date, rule.Amount, rule.FromAccountId, rule.ToAccountId, rule.CategoryId, goalId,
                    TransactionType(rule.FromAccountId, rule.ToAccountId), "RECURRING", "PLANNED",
                    null, rule.Id, rule.Description));
            }
        }

        events = events.Where(item => item.Date <= horizonEnd).OrderBy(item => item.Date).ToList();

        var runningBalances = new Dictionary<long, decimal>(balances);
        var trajectory = new List<ForecastTrajectoryPoint>();
        var minimumBalance = accountsAtT0.Sum(account => account.CurrentBalance);
        var minimumDate = asOf;

        foreach (var group in events.GroupBy(item => item.Date).OrderBy(group => group.Key))
        {
            foreach (var item in group)
                ApplyEffect(runningBalances, item.FromAccountId, item.ToAccountId, item.Amount);

            var total = runningBalances.Values.Sum();
            trajectory.Add(new ForecastTrajectoryPoint(group.Key, new Dictionary<long, decimal>(runningBalances), total));
            if (total < minimumBalance) { minimumBalance = total; minimumDate = group.Key; }
        }

        return new ForecastResult(asOf, horizonEnd, accountsAtT0, events, trajectory, minimumBalance, minimumDate, minimumBalance < 0);
    }

    private static void ApplyEffect(Dictionary<long, decimal> balances, long? fromAccountId, long? toAccountId, decimal amount)
    {
        if (fromAccountId is { } from && balances.ContainsKey(from)) balances[from] -= amount;
        if (toAccountId is { } to && balances.ContainsKey(to)) balances[to] += amount;
    }

    private static string TransactionType(long? fromAccountId, long? toAccountId) =>
        fromAccountId is null ? "INCOME" : toAccountId is null ? "EXPENSE" : "TRANSFER";
}
