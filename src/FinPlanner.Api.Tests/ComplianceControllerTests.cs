using FinPlanner.Api.Contracts;
using FinPlanner.Api.Controllers;
using FinPlanner.Api.Domain;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FinPlanner.Api.Tests;

public class ComplianceControllerTests
{
    private static async Task<Category> AddCategoryAsync(Data.FinPlannerDbContext db, long familyId, string name, CategoryType type)
    {
        var category = new Category { FamilyId = familyId, Name = name, Type = type, Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    [Fact]
    public async Task Get_UsesFamilyDefaultPlan_IgnoresNonDefaultPlanData()
    {
        await using var db = TestDb.CreateContext();
        var (family, defaultPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var category = await AddCategoryAsync(db, family.Id, "Rent", CategoryType.Expense);
        var otherPlan = await TestDb.AddPlanAsync(db, family.Id, "New car");
        var currentUser = new CurrentUserContext { FamilyId = family.Id };
        var planning = new PlanningController(db, currentUser);

        await planning.CreatePlanned(new CreatePlannedTransactionRequest(new DateOnly(2026, 7, 5), 700m, account.Id, null, category.Id, null, "Default plan rent"), CancellationToken.None);
        await planning.CreatePlanned(new CreatePlannedTransactionRequest(new DateOnly(2026, 7, 5), 9999m, account.Id, null, category.Id, null, "Other plan item", otherPlan.Id), CancellationToken.None);

        var controller = new ComplianceController(db, currentUser);
        var result = await controller.Get(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), CancellationToken.None);

        var response = Assert.IsType<PlanComplianceResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(defaultPlan.Id, response.PlanId);
        var cell = Assert.Single(response.Periods.Single().Categories);
        Assert.Equal(700m, cell.PlannedAmount); // never 9999 from the other plan
    }

    [Fact]
    public async Task Get_TimingShift_PlannedJuly31ActualAugust2_AttributedToJuly()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var category = await AddCategoryAsync(db, family.Id, "Mortgage", CategoryType.Expense);
        var currentUser = new CurrentUserContext { FamilyId = family.Id };
        var planning = new PlanningController(db, currentUser);
        var transactions = new TransactionsController(db, currentUser);

        var plannedResult = await planning.CreatePlanned(new CreatePlannedTransactionRequest(new DateOnly(2026, 7, 31), 1200m, account.Id, null, category.Id, null, "Mortgage"), CancellationToken.None);
        var planned = Assert.IsType<PlannedTransactionResponse>(Assert.IsType<CreatedResult>(plannedResult.Result).Value);
        await transactions.Create(new CreateTransactionRequest(new DateOnly(2026, 8, 2), 1200m, account.Id, null, category.Id, planned.Id, "Mortgage paid late"), CancellationToken.None);

        var controller = new ComplianceController(db, currentUser);
        var julyOnly = await controller.Get(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), CancellationToken.None);
        var julyResponse = Assert.IsType<PlanComplianceResponse>(Assert.IsType<OkObjectResult>(julyOnly.Result).Value);
        var julyCell = Assert.Single(julyResponse.Periods.Single().Categories);
        Assert.Equal(1200m, julyCell.PlannedAmount);
        Assert.Equal(1200m, julyCell.ActualAmount);
        Assert.Equal(0m, julyCell.Variance);
        Assert.Equal(2, julyCell.AverageTimingVarianceDays);

        var both = await controller.Get(new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 31), CancellationToken.None);
        var bothResponse = Assert.IsType<PlanComplianceResponse>(Assert.IsType<OkObjectResult>(both.Result).Value);
        var august = bothResponse.Periods.Single(period => period.PeriodStart.Month == 8);
        Assert.Empty(august.Categories); // not double counted in August
    }

    [Fact]
    public async Task Get_RecurringRule_NeverDoubleCountedAgainstMaterializedOccurrence()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var category = await AddCategoryAsync(db, family.Id, "Rent", CategoryType.Expense);
        var currentUser = new CurrentUserContext { FamilyId = family.Id };
        var planning = new PlanningController(db, currentUser);

        var ruleResult = await planning.CreateRecurring(new CreateRecurringRuleRequest("Rent", 700m, account.Id, null, category.Id, RecurrenceFrequency.Monthly, 1, new DateOnly(2026, 1, 1), null, "Rent"), CancellationToken.None);
        var rule = Assert.IsType<RecurringRuleResponse>(Assert.IsType<CreatedResult>(ruleResult.Result).Value);
        await planning.CreatePlanned(new CreatePlannedTransactionRequest(new DateOnly(2026, 7, 1), 700m, account.Id, null, category.Id, rule.Id, "Materialized rent"), CancellationToken.None);

        var controller = new ComplianceController(db, currentUser);
        var result = await controller.Get(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), CancellationToken.None);
        var response = Assert.IsType<PlanComplianceResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

        var cell = Assert.Single(response.Periods.Single().Categories);
        Assert.Equal(700m, cell.PlannedAmount);
        Assert.Equal(1, cell.PlannedRealCount);
        Assert.Equal(0, cell.PlannedRecurringCount);
    }

    [Fact]
    public async Task Get_Transfer_NeverAppearsInComplianceReport()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account1 = await TestDb.AddAccountAsync(db, family.Id, "Joint");
        var account2 = await TestDb.AddAccountAsync(db, family.Id, "Savings");
        var currentUser = new CurrentUserContext { FamilyId = family.Id };
        var transactions = new TransactionsController(db, currentUser);
        await transactions.Create(new CreateTransactionRequest(new DateOnly(2026, 7, 1), 500m, account1.Id, account2.Id, null, null, "Move to savings"), CancellationToken.None);

        var controller = new ComplianceController(db, currentUser);
        var result = await controller.Get(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), CancellationToken.None);
        var response = Assert.IsType<PlanComplianceResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Empty(response.Periods.Single().Categories);
    }

    [Fact]
    public async Task Get_CurrentMonth_IsMarkedInProgress_PastMonthClosed()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var currentUser = new CurrentUserContext { FamilyId = family.Id };
        var controller = new ComplianceController(db, currentUser);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await controller.Get(today.AddMonths(-1), today, CancellationToken.None);
        var response = Assert.IsType<PlanComplianceResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal("CLOSED", response.Periods[0].Status);
        Assert.Equal("IN_PROGRESS", response.Periods[1].Status);
    }

    [Fact]
    public async Task Get_DefaultRange_IsTrailingTwelveMonths()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var currentUser = new CurrentUserContext { FamilyId = family.Id };
        var controller = new ComplianceController(db, currentUser);

        var result = await controller.Get(null, null, CancellationToken.None);
        var response = Assert.IsType<PlanComplianceResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal(12, response.Periods.Count);
    }

    [Fact]
    public async Task Get_FromAfterTo_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var controller = new ComplianceController(db, new CurrentUserContext { FamilyId = family.Id });

        var result = await controller.Get(new DateOnly(2026, 8, 1), new DateOnly(2026, 7, 1), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Get_CrossFamilyIsolation_NeverLeaksOtherFamilysCategoriesOrTransactions()
    {
        await using var db = TestDb.CreateContext();
        var (familyA, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family A");
        var (familyB, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family B");
        var accountB = await TestDb.AddAccountAsync(db, familyB.Id);
        var categoryB = await AddCategoryAsync(db, familyB.Id, "Groceries", CategoryType.Expense);
        var planningB = new PlanningController(db, new CurrentUserContext { FamilyId = familyB.Id });
        await planningB.CreatePlanned(new CreatePlannedTransactionRequest(new DateOnly(2026, 7, 5), 500m, accountB.Id, null, categoryB.Id, null, "Family B groceries"), CancellationToken.None);

        var controller = new ComplianceController(db, new CurrentUserContext { FamilyId = familyA.Id });
        var result = await controller.Get(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), CancellationToken.None);
        var response = Assert.IsType<PlanComplianceResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Empty(response.Periods.Single().Categories);
    }

    [Fact]
    public async Task Get_WithoutCurrentUser_ReturnsUnauthorized()
    {
        await using var db = TestDb.CreateContext();
        var controller = new ComplianceController(db, new CurrentUserContext());

        var result = await controller.Get(null, null, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }
}
