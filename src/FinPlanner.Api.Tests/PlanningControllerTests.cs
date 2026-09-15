using FinPlanner.Api.Contracts;
using FinPlanner.Api.Controllers;
using FinPlanner.Api.Domain;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FinPlanner.Api.Tests;

public class PlanningControllerTests
{
    [Fact]
    public async Task CreatePlanned_WithoutPlanId_UsesFamilyDefaultPlan()
    {
        await using var db = TestDb.CreateContext();
        var (family, defaultPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var controller = new PlanningController(db, new CurrentUserContext { FamilyId = family.Id });

        var result = await controller.CreatePlanned(
            new CreatePlannedTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 100m, null, account.Id, null, null, "Salary"),
            CancellationToken.None);

        var created = Assert.IsType<PlannedTransactionResponse>(Assert.IsType<CreatedResult>(result.Result).Value);
        Assert.Equal(defaultPlan.Id, created.PlanId);
    }

    [Fact]
    public async Task CreatePlanned_WithPlanIdFromAnotherFamily_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (familyA, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family A");
        var accountA = await TestDb.AddAccountAsync(db, familyA.Id);
        var (_, familyBPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family B");

        var controller = new PlanningController(db, new CurrentUserContext { FamilyId = familyA.Id });

        var result = await controller.CreatePlanned(
            new CreatePlannedTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 100m, null, accountA.Id, null, null, "Salary", familyBPlan.Id),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("The selected plan does not belong to this family.", badRequest.Value);

        // Nothing should have been persisted for the rejected cross-tenant attempt.
        Assert.Empty(db.PlannedTransactions);
    }

    [Fact]
    public async Task CreatePlanned_WithExplicitNonDefaultPlanInSameFamily_Succeeds()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family A");
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var controller = new PlansController(db, new CurrentUserContext { FamilyId = family.Id });
        var otherPlanResult = await controller.Create(new CreatePlanRequest("New car", null), CancellationToken.None);
        var otherPlan = Assert.IsType<PlanResponse>(Assert.IsType<CreatedResult>(otherPlanResult.Result).Value);

        var planningController = new PlanningController(db, new CurrentUserContext { FamilyId = family.Id });
        var result = await planningController.CreatePlanned(
            new CreatePlannedTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 50m, account.Id, null, null, null, "Extra car cost", otherPlan.Id),
            CancellationToken.None);

        var created = Assert.IsType<PlannedTransactionResponse>(Assert.IsType<CreatedResult>(result.Result).Value);
        Assert.Equal(otherPlan.Id, created.PlanId);
    }

    [Fact]
    public async Task PlannedTransactions_List_OnlyReturnsItemsFromTheResolvedPlan()
    {
        await using var db = TestDb.CreateContext();
        var (family, defaultPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var plansController = new PlansController(db, new CurrentUserContext { FamilyId = family.Id });
        var otherPlanResult = await plansController.Create(new CreatePlanRequest("New car", null), CancellationToken.None);
        var otherPlan = Assert.IsType<PlanResponse>(Assert.IsType<CreatedResult>(otherPlanResult.Result).Value);

        var planningController = new PlanningController(db, new CurrentUserContext { FamilyId = family.Id });
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await planningController.CreatePlanned(new CreatePlannedTransactionRequest(today, 10m, null, account.Id, null, null, "Default plan item"), CancellationToken.None);
        await planningController.CreatePlanned(new CreatePlannedTransactionRequest(today, 20m, null, account.Id, null, null, "Other plan item", otherPlan.Id), CancellationToken.None);

        var defaultList = await planningController.PlannedTransactions(null, null, null, null, CancellationToken.None);
        var defaultItems = Assert.IsType<List<PlannedTransactionResponse>>(Assert.IsType<OkObjectResult>(defaultList.Result).Value);
        Assert.Single(defaultItems);
        Assert.Equal("Default plan item", defaultItems[0].Description);
        Assert.Equal(defaultPlan.Id, defaultItems[0].PlanId);

        var otherList = await planningController.PlannedTransactions(null, null, null, otherPlan.Id, CancellationToken.None);
        var otherItems = Assert.IsType<List<PlannedTransactionResponse>>(Assert.IsType<OkObjectResult>(otherList.Result).Value);
        Assert.Single(otherItems);
        Assert.Equal("Other plan item", otherItems[0].Description);
    }

    [Fact]
    public async Task CreatePlanned_WithGoalFromAnotherPlanInSameFamily_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (family, defaultPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var otherPlan = await TestDb.AddPlanAsync(db, family.Id, "New car");
        var goalOnOtherPlan = await TestDb.AddGoalAsync(db, family.Id, otherPlan.Id);

        var controller = new PlanningController(db, new CurrentUserContext { FamilyId = family.Id });
        var result = await controller.CreatePlanned(
            new CreatePlannedTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 100m, account.Id, null, null, null, "Contribution", defaultPlan.Id, goalOnOtherPlan.Id),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("The selected goal does not belong to this plan.", badRequest.Value);
        Assert.Empty(db.PlannedTransactions);
    }

    [Fact]
    public async Task CreatePlanned_WithGoalFromSamePlan_Succeeds()
    {
        await using var db = TestDb.CreateContext();
        var (family, defaultPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var goal = await TestDb.AddGoalAsync(db, family.Id, defaultPlan.Id);

        var controller = new PlanningController(db, new CurrentUserContext { FamilyId = family.Id });
        var result = await controller.CreatePlanned(
            new CreatePlannedTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 100m, account.Id, null, null, null, "Contribution", defaultPlan.Id, goal.Id),
            CancellationToken.None);

        var created = Assert.IsType<PlannedTransactionResponse>(Assert.IsType<CreatedResult>(result.Result).Value);
        Assert.Equal(goal.Id, created.GoalId);
    }

    [Fact]
    public async Task CreateRecurring_WithoutPlanId_UsesFamilyDefaultPlan()
    {
        await using var db = TestDb.CreateContext();
        var (family, defaultPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var controller = new PlanningController(db, new CurrentUserContext { FamilyId = family.Id });

        var result = await controller.CreateRecurring(
            new CreateRecurringRuleRequest("Salary", 3500m, null, account.Id, null, RecurrenceFrequency.Monthly, 1, DateOnly.FromDateTime(DateTime.UtcNow), null, null),
            CancellationToken.None);

        var created = Assert.IsType<RecurringRuleResponse>(Assert.IsType<CreatedResult>(result.Result).Value);
        Assert.Equal(defaultPlan.Id, created.PlanId);
    }

    [Fact]
    public async Task CreateRecurring_IncomeShape_ReportsIncomeTransactionType()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var controller = new PlanningController(db, new CurrentUserContext { FamilyId = family.Id });

        var result = await controller.CreateRecurring(
            new CreateRecurringRuleRequest("Salary", 3500m, null, account.Id, null, RecurrenceFrequency.Monthly, 1, DateOnly.FromDateTime(DateTime.UtcNow), null, null),
            CancellationToken.None);

        var created = Assert.IsType<RecurringRuleResponse>(Assert.IsType<CreatedResult>(result.Result).Value);
        Assert.Equal("INCOME", created.TransactionType);
    }

    [Fact]
    public async Task UpdateRecurring_ChangesFieldsAndReturnsUpdatedResponse()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var controller = new PlanningController(db, new CurrentUserContext { FamilyId = family.Id });
        var createResult = await controller.CreateRecurring(
            new CreateRecurringRuleRequest("Rent", 700m, account.Id, null, null, RecurrenceFrequency.Monthly, 1, DateOnly.FromDateTime(DateTime.UtcNow), null, null),
            CancellationToken.None);
        var created = Assert.IsType<RecurringRuleResponse>(Assert.IsType<CreatedResult>(createResult.Result).Value);

        var updateResult = await controller.UpdateRecurring(created.Id,
            new UpdateRecurringRuleRequest("Rent (updated)", 750m, account.Id, null, null, RecurrenceFrequency.Monthly, 5, DateOnly.FromDateTime(DateTime.UtcNow), null, "Landlord raised rent"),
            CancellationToken.None);

        var updated = Assert.IsType<RecurringRuleResponse>(Assert.IsType<OkObjectResult>(updateResult.Result).Value);
        Assert.Equal("Rent (updated)", updated.Name);
        Assert.Equal(750m, updated.Amount);
        Assert.Equal((short)5, updated.DayOfMonth);
        Assert.Equal("Landlord raised rent", updated.Description);
        Assert.Equal(created.PlanId, updated.PlanId); // PlanId never changes via update
    }

    [Fact]
    public async Task UpdateRecurring_FromAnotherFamily_ReturnsNotFound()
    {
        await using var db = TestDb.CreateContext();
        var (familyA, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family A");
        var accountA = await TestDb.AddAccountAsync(db, familyA.Id);
        var controllerA = new PlanningController(db, new CurrentUserContext { FamilyId = familyA.Id });
        var createResult = await controllerA.CreateRecurring(
            new CreateRecurringRuleRequest("Rent", 700m, accountA.Id, null, null, RecurrenceFrequency.Monthly, 1, DateOnly.FromDateTime(DateTime.UtcNow), null, null),
            CancellationToken.None);
        var created = Assert.IsType<RecurringRuleResponse>(Assert.IsType<CreatedResult>(createResult.Result).Value);

        var (familyB, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family B");
        var controllerB = new PlanningController(db, new CurrentUserContext { FamilyId = familyB.Id });

        var result = await controllerB.UpdateRecurring(created.Id,
            new UpdateRecurringRuleRequest("Hijacked", 1m, accountA.Id, null, null, RecurrenceFrequency.Monthly, 1, DateOnly.FromDateTime(DateTime.UtcNow), null, null),
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task DeleteRecurring_SoftDeactivates_ExcludedFromDefaultListing_VisibleWithIncludeInactive()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var controller = new PlanningController(db, new CurrentUserContext { FamilyId = family.Id });
        var createResult = await controller.CreateRecurring(
            new CreateRecurringRuleRequest("Gym", 40m, account.Id, null, null, RecurrenceFrequency.Monthly, 1, DateOnly.FromDateTime(DateTime.UtcNow), null, null),
            CancellationToken.None);
        var created = Assert.IsType<RecurringRuleResponse>(Assert.IsType<CreatedResult>(createResult.Result).Value);

        var deleteResult = await controller.DeleteRecurring(created.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(deleteResult);

        var defaultList = await controller.RecurringRules(null, false, CancellationToken.None);
        var defaultItems = Assert.IsType<List<RecurringRuleResponse>>(Assert.IsType<OkObjectResult>(defaultList.Result).Value);
        Assert.Empty(defaultItems);

        var fullList = await controller.RecurringRules(null, true, CancellationToken.None);
        var fullItems = Assert.IsType<List<RecurringRuleResponse>>(Assert.IsType<OkObjectResult>(fullList.Result).Value);
        var inactive = Assert.Single(fullItems);
        Assert.False(inactive.Active);
    }

    [Fact]
    public async Task DeleteRecurring_FromAnotherFamily_ReturnsNotFound()
    {
        await using var db = TestDb.CreateContext();
        var (familyA, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family A");
        var accountA = await TestDb.AddAccountAsync(db, familyA.Id);
        var controllerA = new PlanningController(db, new CurrentUserContext { FamilyId = familyA.Id });
        var createResult = await controllerA.CreateRecurring(
            new CreateRecurringRuleRequest("Rent", 700m, accountA.Id, null, null, RecurrenceFrequency.Monthly, 1, DateOnly.FromDateTime(DateTime.UtcNow), null, null),
            CancellationToken.None);
        var created = Assert.IsType<RecurringRuleResponse>(Assert.IsType<CreatedResult>(createResult.Result).Value);

        var (familyB, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family B");
        var controllerB = new PlanningController(db, new CurrentUserContext { FamilyId = familyB.Id });

        var result = await controllerB.DeleteRecurring(created.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
