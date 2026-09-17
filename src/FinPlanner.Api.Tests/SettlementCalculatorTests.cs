using FinPlanner.Api.Domain;
using FinPlanner.Api.Reporting;
using Xunit;

namespace FinPlanner.Api.Tests;

public class SettlementCalculatorTests
{
    private static User MakeUser(long id, string name) =>
        new() { Id = id, FamilyId = 1, Name = name, Email = $"{name.ToLowerInvariant()}-{id}@test.local", PasswordHash = "test-placeholder-hash", Role = UserRole.Member, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static Account MakeAccount(long id, string name, long? ownerUserId) =>
        new() { Id = id, FamilyId = 1, Name = name, Currency = "EUR", OpeningBalance = 0, OwnerUserId = ownerUserId, Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static Category MakeCategory(long id, string name, CategoryType type = CategoryType.Expense) =>
        new() { Id = id, FamilyId = 1, Name = name, Type = type, Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static ActualTransaction MakeExpense(long id, DateOnly date, decimal amount, long fromAccountId, long? categoryId) =>
        new() { Id = id, FamilyId = 1, ActualDate = date, Amount = amount, FromAccountId = fromAccountId, ToAccountId = null, CategoryId = categoryId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static ActualTransaction MakeIncome(long id, DateOnly date, decimal amount, long toAccountId) =>
        new() { Id = id, FamilyId = 1, ActualDate = date, Amount = amount, FromAccountId = null, ToAccountId = toAccountId, CategoryId = null, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static ActualTransaction MakeTransfer(long id, DateOnly date, decimal amount, long fromAccountId, long toAccountId) =>
        new() { Id = id, FamilyId = 1, ActualDate = date, Amount = amount, FromAccountId = fromAccountId, ToAccountId = toAccountId, CategoryId = null, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static ExpenseAllocationRule MakeRule(long id, long? categoryId, ExpenseAllocationMethod method) =>
        new() { Id = id, FamilyId = 1, CategoryId = categoryId, Method = method, Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static ExpenseAllocationShare MakeShare(long id, long ruleId, long userId, decimal? percentage = null, decimal? fixedAmount = null) =>
        new() { Id = id, FamilyId = 1, AllocationRuleId = ruleId, UserId = userId, Percentage = percentage, FixedAmount = fixedAmount };

    [Fact]
    public void PartE_WorkedExample_IncomeRatio_WithPoolContribution_MatchesHandComputedNumbers()
    {
        var userA = MakeUser(1, "A");
        var userB = MakeUser(2, "B");
        var salaryA = MakeAccount(1, "Salary A", ownerUserId: 1);
        var salaryB = MakeAccount(2, "Salary B", ownerUserId: 2);
        var mortgageAccount = MakeAccount(3, "Mortgage", ownerUserId: null); // pooled/joint
        var household = MakeCategory(1, "Household");
        var rule = MakeRule(1, household.Id, ExpenseAllocationMethod.IncomeRatio);
        var shares = new List<ExpenseAllocationShare> { MakeShare(1, rule.Id, 1), MakeShare(2, rule.Id, 2) };

        // Trailing 3-month income: A=6000, B=9000 -> derived ratio 40/60, matching CLAUDE.md's example exactly.
        var incomeByUser = new Dictionary<long, decimal> { [1] = 6000m, [2] = 9000m };

        var actuals = new List<ActualTransaction>
        {
            MakeExpense(1, new DateOnly(2026, 9, 5), 1200m, mortgageAccount.Id, household.Id),   // Mortgage
            MakeExpense(2, new DateOnly(2026, 9, 6), 150m, mortgageAccount.Id, household.Id),    // Electricity
            MakeExpense(3, new DateOnly(2026, 9, 7), 30m, salaryA.Id, household.Id),              // Internet, paid personally by A
            MakeExpense(4, new DateOnly(2026, 9, 8), 600m, mortgageAccount.Id, household.Id),    // Groceries
        };

        var result = SettlementCalculator.Build(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30),
            [userA, userB], [salaryA, salaryB, mortgageAccount], [household], actuals, [rule], shares, incomeByUser);

        var a = result.Users.Single(u => u.UserId == 1);
        var b = result.Users.Single(u => u.UserId == 2);

        Assert.Equal(792m, a.TotalExpectedContribution);
        Assert.Equal(1188m, b.TotalExpectedContribution);
        Assert.Equal(30m, a.TotalPaid);
        Assert.Equal(0m, b.TotalPaid);
        Assert.Equal(-762m, a.Settlement);
        Assert.Equal(-1188m, b.Settlement);
        Assert.Equal(1950m, result.PoolContribution);
        Assert.Equal(0m, result.UnattributedAmount);

        // I-A2: sum of settlements + pool contribution = 0.
        Assert.Equal(0m, a.Settlement + b.Settlement + result.PoolContribution);
        // I-A1: expected contributions across users sum to total spend.
        Assert.Equal(1980m, a.TotalExpectedContribution + b.TotalExpectedContribution);
    }

    [Fact]
    public void UnallocatedExpense_PaidFromPooledAccount_GoesToUnattributedAmount_NotPoolContribution()
    {
        var userA = MakeUser(1, "A");
        var pooled = MakeAccount(1, "Joint", ownerUserId: null);
        var miscCategory = MakeCategory(1, "Misc"); // no rule for this category, no family default either
        var actual = MakeExpense(1, new DateOnly(2026, 7, 10), 75m, pooled.Id, miscCategory.Id);

        var result = SettlementCalculator.Build(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [userA], [pooled], [miscCategory], [actual], [], [], new Dictionary<long, decimal>());

        Assert.Equal(75m, result.UnattributedAmount);
        Assert.Equal(0m, result.PoolContribution);
        var a = result.Users.Single();
        Assert.Equal(0m, a.TotalPaid);
        Assert.Equal(0m, a.TotalExpectedContribution);
        // Invariant still holds even with an unattributed expense present.
        Assert.Equal(0m, a.Settlement + result.PoolContribution);
    }

    [Fact]
    public void UnallocatedExpense_PaidFromOwnedAccount_NetsToZero_FallbackIsPayerBears100Percent()
    {
        var userA = MakeUser(1, "A");
        var ownedByA = MakeAccount(1, "Salary A", ownerUserId: 1);
        var personalCategory = MakeCategory(1, "Personal");
        var actual = MakeExpense(1, new DateOnly(2026, 7, 10), 50m, ownedByA.Id, personalCategory.Id);

        var result = SettlementCalculator.Build(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [userA], [ownedByA], [personalCategory], [actual], [], [], new Dictionary<long, decimal>());

        var a = result.Users.Single();
        Assert.Equal(50m, a.TotalPaid);
        Assert.Equal(50m, a.TotalExpectedContribution);
        Assert.Equal(0m, a.Settlement);
        Assert.Equal(0m, result.UnattributedAmount);
    }

    [Fact]
    public void FixedPercentage_LargestRemainder_SharesSumExactlyToAmount()
    {
        var userA = MakeUser(1, "A");
        var userB = MakeUser(2, "B");
        var userC = MakeUser(3, "C");
        var pooled = MakeAccount(1, "Joint", ownerUserId: null);
        var category = MakeCategory(1, "Groceries");
        var rule = MakeRule(1, category.Id, ExpenseAllocationMethod.FixedPercentage);
        // 33.33/33.33/33.34 would be the naive split; exact thirds of 100 require a remainder-cent fix-up.
        var shares = new List<ExpenseAllocationShare> { MakeShare(1, rule.Id, 1, percentage: 33.33m), MakeShare(2, rule.Id, 2, percentage: 33.33m), MakeShare(3, rule.Id, 3, percentage: 33.34m) };
        var actual = MakeExpense(1, new DateOnly(2026, 7, 1), 100m, pooled.Id, category.Id);

        var result = SettlementCalculator.Build(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [userA, userB, userC], [pooled], [category], [actual], [rule], shares, new Dictionary<long, decimal>());

        var sumExpected = result.Users.Sum(u => u.TotalExpectedContribution);
        Assert.Equal(100m, sumExpected);
    }

    [Fact]
    public void Equal_ThreeWaySplitOfOddAmount_SharesSumExactlyToAmount()
    {
        var users = new[] { MakeUser(1, "A"), MakeUser(2, "B"), MakeUser(3, "C") };
        var pooled = MakeAccount(1, "Joint", ownerUserId: null);
        var category = MakeCategory(1, "Groceries");
        var rule = MakeRule(1, category.Id, ExpenseAllocationMethod.Equal);
        var shares = new List<ExpenseAllocationShare> { MakeShare(1, rule.Id, 1), MakeShare(2, rule.Id, 2), MakeShare(3, rule.Id, 3) };
        var actual = MakeExpense(1, new DateOnly(2026, 7, 1), 10m, pooled.Id, category.Id); // 10/3 = 3.333...

        var result = SettlementCalculator.Build(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            users, [pooled], [category], [actual], [rule], shares, new Dictionary<long, decimal>());

        Assert.Equal(10m, result.Users.Sum(u => u.TotalExpectedContribution));
        // Every user gets either 3.33 or 3.34 — never a lopsided split.
        Assert.All(result.Users, u => Assert.True(u.TotalExpectedContribution is 3.33m or 3.34m));
    }

    [Fact]
    public void IncomeRatio_ZeroTotalIncome_FallsBackToEqual_NoCrash()
    {
        var users = new[] { MakeUser(1, "A"), MakeUser(2, "B") };
        var pooled = MakeAccount(1, "Joint", ownerUserId: null);
        var category = MakeCategory(1, "Household");
        var rule = MakeRule(1, category.Id, ExpenseAllocationMethod.IncomeRatio);
        var shares = new List<ExpenseAllocationShare> { MakeShare(1, rule.Id, 1), MakeShare(2, rule.Id, 2) };
        var actual = MakeExpense(1, new DateOnly(2026, 7, 1), 100m, pooled.Id, category.Id);
        var zeroIncome = new Dictionary<long, decimal> { [1] = 0m, [2] = 0m };

        var result = SettlementCalculator.Build(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            users, [pooled], [category], [actual], [rule], shares, zeroIncome);

        Assert.All(result.Users, u => Assert.Equal(50m, u.TotalExpectedContribution));
    }

    [Fact]
    public void FixedAmount_ConfiguredSumDoesNotMatchActualAmount_LowestUserIdAbsorbsDifference()
    {
        var users = new[] { MakeUser(5, "B"), MakeUser(2, "A") }; // deliberately out of id order
        var pooled = MakeAccount(1, "Joint", ownerUserId: null);
        var category = MakeCategory(1, "Mortgage");
        var rule = MakeRule(1, category.Id, ExpenseAllocationMethod.FixedAmount);
        var shares = new List<ExpenseAllocationShare> { MakeShare(1, rule.Id, 5, fixedAmount: 480m), MakeShare(2, rule.Id, 2, fixedAmount: 720m) };
        // Actual mortgage was 1204.50, not the assumed round 1200.
        var actual = MakeExpense(1, new DateOnly(2026, 7, 1), 1204.50m, pooled.Id, category.Id);

        var result = SettlementCalculator.Build(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            users, [pooled], [category], [actual], [rule], shares, new Dictionary<long, decimal>());

        var lowestIdUser = result.Users.Single(u => u.UserId == 2); // user id 2 is the lowest of {5,2}
        var otherUser = result.Users.Single(u => u.UserId == 5);
        Assert.Equal(724.50m, lowestIdUser.TotalExpectedContribution); // 720 + 4.50 remainder
        Assert.Equal(480m, otherUser.TotalExpectedContribution);
        Assert.Equal(1204.50m, result.Users.Sum(u => u.TotalExpectedContribution));
    }

    [Fact]
    public void CategorySpecificRule_TakesPrecedenceOverFamilyDefault()
    {
        var users = new[] { MakeUser(1, "A"), MakeUser(2, "B") };
        var pooled = MakeAccount(1, "Joint", ownerUserId: null);
        var groceries = MakeCategory(1, "Groceries");
        var categoryRule = MakeRule(1, groceries.Id, ExpenseAllocationMethod.Equal);
        var defaultRule = MakeRule(2, null, ExpenseAllocationMethod.FixedPercentage);
        var categoryShares = new List<ExpenseAllocationShare> { MakeShare(1, categoryRule.Id, 1), MakeShare(2, categoryRule.Id, 2) };
        var defaultShares = new List<ExpenseAllocationShare> { MakeShare(3, defaultRule.Id, 1, percentage: 100m), MakeShare(4, defaultRule.Id, 2, percentage: 0m) };
        var actual = MakeExpense(1, new DateOnly(2026, 7, 1), 100m, pooled.Id, groceries.Id);

        var result = SettlementCalculator.Build(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            users, [pooled], [groceries], [actual], [categoryRule, defaultRule], [.. categoryShares, .. defaultShares], new Dictionary<long, decimal>());

        // Equal (category rule) wins, not the 100/0 family default.
        Assert.All(result.Users, u => Assert.Equal(50m, u.TotalExpectedContribution));
    }

    [Fact]
    public void Transfer_AndIncome_AreNeverAllocated()
    {
        var userA = MakeUser(1, "A");
        var accountA = MakeAccount(1, "A", ownerUserId: 1);
        var accountB = MakeAccount(2, "B", ownerUserId: null);
        var category = MakeCategory(1, "Household");
        var rule = MakeRule(1, null, ExpenseAllocationMethod.Equal);
        var shares = new List<ExpenseAllocationShare> { MakeShare(1, rule.Id, 1) };
        var transfer = MakeTransfer(1, new DateOnly(2026, 7, 1), 500m, accountA.Id, accountB.Id);
        var income = MakeIncome(2, new DateOnly(2026, 7, 1), 3000m, accountA.Id);

        var result = SettlementCalculator.Build(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [userA], [accountA, accountB], [category], [transfer, income], [rule], shares, new Dictionary<long, decimal>());

        Assert.Equal(0m, result.Users.Single().TotalExpectedContribution);
        Assert.Equal(0m, result.Users.Single().TotalPaid);
        Assert.Equal(0m, result.PoolContribution);
        Assert.Equal(0m, result.UnattributedAmount);
    }

    [Fact]
    public void MultiplePayers_SplitAcrossTwoTransactionRows_BothSumCorrectly()
    {
        var users = new[] { MakeUser(1, "A"), MakeUser(2, "B") };
        var accountA = MakeAccount(1, "A", ownerUserId: 1);
        var accountB = MakeAccount(2, "B", ownerUserId: 2);
        var category = MakeCategory(1, "Vacation");
        var rule = MakeRule(1, category.Id, ExpenseAllocationMethod.Equal);
        var shares = new List<ExpenseAllocationShare> { MakeShare(1, rule.Id, 1), MakeShare(2, rule.Id, 2) };
        var half1 = MakeExpense(1, new DateOnly(2026, 7, 1), 300m, accountA.Id, category.Id);
        var half2 = MakeExpense(2, new DateOnly(2026, 7, 1), 300m, accountB.Id, category.Id);

        var result = SettlementCalculator.Build(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            users, [accountA, accountB], [category], [half1, half2], [rule], shares, new Dictionary<long, decimal>());

        var a = result.Users.Single(u => u.UserId == 1);
        var b = result.Users.Single(u => u.UserId == 2);
        Assert.Equal(300m, a.TotalPaid);
        Assert.Equal(300m, b.TotalPaid);
        Assert.Equal(300m, a.TotalExpectedContribution);
        Assert.Equal(300m, b.TotalExpectedContribution);
        Assert.Equal(0m, a.Settlement);
        Assert.Equal(0m, b.Settlement);
    }

    [Fact]
    public void ThreeUsers_PositionsGeneralizeCorrectly_NoDebtNettingAttempted()
    {
        var users = new[] { MakeUser(1, "A"), MakeUser(2, "B"), MakeUser(3, "C") };
        var pooled = MakeAccount(1, "Joint", ownerUserId: null);
        var category = MakeCategory(1, "Rent");
        var rule = MakeRule(1, category.Id, ExpenseAllocationMethod.Equal);
        var shares = new List<ExpenseAllocationShare> { MakeShare(1, rule.Id, 1), MakeShare(2, rule.Id, 2), MakeShare(3, rule.Id, 3) };
        var actual = MakeExpense(1, new DateOnly(2026, 7, 1), 900m, pooled.Id, category.Id);

        var result = SettlementCalculator.Build(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            users, [pooled], [category], [actual], [rule], shares, new Dictionary<long, decimal>());

        Assert.Equal(3, result.Users.Count);
        Assert.All(result.Users, u => Assert.Equal(300m, u.TotalExpectedContribution));
        Assert.All(result.Users, u => Assert.Equal(-300m, u.Settlement));
        Assert.Equal(900m, result.PoolContribution);
        Assert.Equal(0m, result.Users.Sum(u => u.Settlement) + result.PoolContribution);
    }

    [Fact]
    public void DateOutsideRange_IsExcluded()
    {
        var userA = MakeUser(1, "A");
        var pooled = MakeAccount(1, "Joint", ownerUserId: null);
        var category = MakeCategory(1, "Groceries");
        var rule = MakeRule(1, category.Id, ExpenseAllocationMethod.Equal);
        var shares = new List<ExpenseAllocationShare> { MakeShare(1, rule.Id, 1) };
        var outsideRange = MakeExpense(1, new DateOnly(2026, 8, 1), 100m, pooled.Id, category.Id);

        var result = SettlementCalculator.Build(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31),
            [userA], [pooled], [category], [outsideRange], [rule], shares, new Dictionary<long, decimal>());

        Assert.Equal(0m, result.PoolContribution);
        Assert.Empty(result.Categories);
    }
}
