using FinPlanner.Api.Domain;

namespace FinPlanner.Api.Reporting;

public sealed record UserSettlement(long UserId, string Name, decimal TotalPaid, decimal TotalExpectedContribution, decimal Settlement);

public sealed record CategorySettlement(long? CategoryId, string CategoryName, string Method, decimal TotalAmount, IReadOnlyDictionary<long, decimal> PerUserExpected);

public sealed record SettlementResult(
    DateOnly From, DateOnly To, IReadOnlyList<UserSettlement> Users, decimal PoolContribution, decimal UnattributedAmount, IReadOnlyList<CategorySettlement> Categories);

/// <summary>
/// Pure reporting/analytics layer over Account/Category/ActualTransaction (Part L). Never touches
/// ForecastCalculator, AvailableToSpendCalculator or PlanComplianceCalculator, and never produces or
/// requires any actual transaction — settlement is always a freshly computed number (Part L §12, decision #6).
/// Family-scoped, actuals-only (Part L §9): PlannedTransaction never participates.
/// </summary>
public static class SettlementCalculator
{
    private sealed class CategoryAccumulator
    {
        public required string Name;
        public required string Method;
        public decimal Total;
        public readonly Dictionary<long, decimal> PerUser = new();
    }

    public static SettlementResult Build(
        DateOnly from,
        DateOnly to,
        IReadOnlyList<User> users,
        IReadOnlyList<Account> accounts,
        IReadOnlyList<Category> categories,
        IReadOnlyList<ActualTransaction> actualTransactions,
        IReadOnlyList<ExpenseAllocationRule> rules,
        IReadOnlyList<ExpenseAllocationShare> shares,
        IReadOnlyDictionary<long, decimal> trailingIncomeByUserId)
    {
        var ownerByAccountId = accounts.ToDictionary(account => account.Id, account => account.OwnerUserId);
        var categoryById = categories.ToDictionary(category => category.Id);
        var categoryRuleByCategoryId = rules.Where(rule => rule.Active && rule.CategoryId is not null).ToDictionary(rule => rule.CategoryId!.Value);
        var familyDefaultRule = rules.FirstOrDefault(rule => rule.Active && rule.CategoryId is null);
        var sharesByRuleId = shares.ToLookup(share => share.AllocationRuleId);

        var totalPaid = users.ToDictionary(user => user.Id, _ => 0m);
        var totalExpected = users.ToDictionary(user => user.Id, _ => 0m);
        var poolContribution = 0m;
        var unattributedAmount = 0m;
        var categoryTotals = new Dictionary<long?, CategoryAccumulator>();

        // Only EXPENSE-shaped transactions are allocated — never transfers or income (I-A4).
        var expenses = actualTransactions.Where(item =>
            item.ActualDate >= from && item.ActualDate <= to && item.FromAccountId is not null && item.ToAccountId is null);

        foreach (var expense in expenses)
        {
            var ownerUserId = ownerByAccountId.TryGetValue(expense.FromAccountId!.Value, out var owner) ? owner : null;
            var rule = expense.CategoryId is { } categoryId && categoryRuleByCategoryId.TryGetValue(categoryId, out var categoryRule)
                ? categoryRule
                : familyDefaultRule;

            if (rule is null)
            {
                // I-A3 fallback: no category-specific or family-default rule.
                if (ownerUserId is { } payerId)
                {
                    // 100% to whoever physically paid — nets to zero for this expense (paid == expected).
                    totalPaid[payerId] = totalPaid.GetValueOrDefault(payerId) + expense.Amount;
                    totalExpected[payerId] = totalExpected.GetValueOrDefault(payerId) + expense.Amount;
                }
                else
                {
                    // Unallocated AND paid from a pooled account: excluded entirely from the settlement
                    // math (not pool_contribution, not anyone's expected/paid) — Part L §7's corrected I-A2.
                    unattributedAmount += expense.Amount;
                }
                continue;
            }

            var ruleShares = sharesByRuleId[rule.Id].ToList();
            var expectedShares = ComputeExpectedShares(rule.Method, ruleShares, expense.Amount, trailingIncomeByUserId);
            foreach (var (userId, share) in expectedShares)
                totalExpected[userId] = totalExpected.GetValueOrDefault(userId) + share;

            if (ownerUserId is { } owningUserId)
                totalPaid[owningUserId] = totalPaid.GetValueOrDefault(owningUserId) + expense.Amount;
            else
                poolContribution += expense.Amount;

            var categoryKey = expense.CategoryId;
            if (!categoryTotals.TryGetValue(categoryKey, out var bucket))
            {
                var categoryName = categoryKey is { } id && categoryById.TryGetValue(id, out var category) ? category.Name : "Family default";
                bucket = new CategoryAccumulator { Name = categoryName, Method = rule.Method.ToString() };
                categoryTotals[categoryKey] = bucket;
            }
            bucket.Total += expense.Amount;
            foreach (var (userId, share) in expectedShares)
                bucket.PerUser[userId] = bucket.PerUser.GetValueOrDefault(userId) + share;
        }

        var userResults = users.Select(user => new UserSettlement(
            user.Id, user.Name, totalPaid[user.Id], totalExpected[user.Id], totalPaid[user.Id] - totalExpected[user.Id])).ToList();

        var categoryResults = categoryTotals.Select(entry => new CategorySettlement(
            entry.Key, entry.Value.Name, entry.Value.Method, entry.Value.Total, entry.Value.PerUser)).ToList();

        return new SettlementResult(from, to, userResults, poolContribution, unattributedAmount, categoryResults);
    }

    private static Dictionary<long, decimal> ComputeExpectedShares(
        ExpenseAllocationMethod method, IReadOnlyList<ExpenseAllocationShare> shares, decimal amount, IReadOnlyDictionary<long, decimal> trailingIncomeByUserId)
    {
        var userIds = shares.Select(share => share.UserId).OrderBy(id => id).ToList();
        if (userIds.Count == 0) return new Dictionary<long, decimal>();

        switch (method)
        {
            case ExpenseAllocationMethod.FixedPercentage:
                return AllocateWithLargestRemainder(amount, userIds, id => amount * (ShareFor(shares, id).Percentage ?? 0m) / 100m);

            case ExpenseAllocationMethod.IncomeRatio:
            {
                var totalIncome = userIds.Sum(id => trailingIncomeByUserId.GetValueOrDefault(id));
                // Falls back to EQUAL when total income is zero for the period (no crash) — Part E §7.
                return totalIncome > 0
                    ? AllocateWithLargestRemainder(amount, userIds, id => amount * trailingIncomeByUserId.GetValueOrDefault(id) / totalIncome)
                    : AllocateWithLargestRemainder(amount, userIds, _ => amount / userIds.Count);
            }

            case ExpenseAllocationMethod.FixedAmount:
            {
                var configured = userIds.ToDictionary(id => id, id => ShareFor(shares, id).FixedAmount ?? 0m);
                var difference = amount - configured.Values.Sum();
                var remainderBearer = userIds.Min(); // lowest user id absorbs the difference — Part E §7.
                configured[remainderBearer] += difference;
                return configured;
            }

            default: // Equal
                return AllocateWithLargestRemainder(amount, userIds, _ => amount / userIds.Count);
        }
    }

    private static ExpenseAllocationShare ShareFor(IReadOnlyList<ExpenseAllocationShare> shares, long userId) =>
        shares.First(share => share.UserId == userId);

    // Largest-remainder rounding: every raw (unrounded) share is floored to whole cents, then the leftover
    // cents (amount*100 minus the sum of floors) are handed out one at a time to the shares with the
    // largest fractional remainder — guaranteeing the rounded shares always sum to exactly `amount` (I-A1).
    private static Dictionary<long, decimal> AllocateWithLargestRemainder(decimal amount, List<long> userIds, Func<long, decimal> rawShareFor)
    {
        var totalCents = (long)Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
        var entries = userIds.Select(id =>
        {
            var rawCents = rawShareFor(id) * 100m;
            var flooredCents = Math.Floor(rawCents);
            return (Id: id, FlooredCents: (long)flooredCents, Remainder: rawCents - flooredCents);
        }).ToList();

        var extraCents = totalCents - entries.Sum(entry => entry.FlooredCents);
        var result = entries.ToDictionary(entry => entry.Id, entry => entry.FlooredCents);
        foreach (var id in entries.OrderByDescending(entry => entry.Remainder).ThenBy(entry => entry.Id)
                     .Take((int)Math.Max(0, extraCents)).Select(entry => entry.Id))
            result[id] += 1;

        return result.ToDictionary(entry => entry.Key, entry => entry.Value / 100m);
    }
}
