using FinPlanner.Api.Contracts;
using FinPlanner.Api.Controllers;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FinPlanner.Api.Tests;

public class TransactionsControllerTests
{
    [Fact]
    public async Task Create_WithGoalFromNonDefaultPlan_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var otherPlan = await TestDb.AddPlanAsync(db, family.Id, "New car");
        var goalOnOtherPlan = await TestDb.AddGoalAsync(db, family.Id, otherPlan.Id);

        var controller = new TransactionsController(db, new CurrentUserContext { FamilyId = family.Id });
        var result = await controller.Create(
            new CreateTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 100m, account.Id, null, null, null, "Contribution", goalOnOtherPlan.Id),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("The selected goal does not belong to the family's default plan.", badRequest.Value);
        Assert.Empty(db.ActualTransactions);
    }

    [Fact]
    public async Task Create_WithGoalFromDefaultPlan_Succeeds()
    {
        await using var db = TestDb.CreateContext();
        var (family, defaultPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var goal = await TestDb.AddGoalAsync(db, family.Id, defaultPlan.Id);

        var controller = new TransactionsController(db, new CurrentUserContext { FamilyId = family.Id });
        var result = await controller.Create(
            new CreateTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 100m, account.Id, null, null, null, "Contribution", goal.Id),
            CancellationToken.None);

        var created = Assert.IsType<TransactionResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(goal.Id, created.GoalId);
    }

    [Fact]
    public async Task Create_MatchedToPlannedTransactionOnDefaultPlan_Succeeds()
    {
        await using var db = TestDb.CreateContext();
        var (family, defaultPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var planning = new PlanningController(db, new CurrentUserContext { FamilyId = family.Id });
        var plannedResult = await planning.CreatePlanned(new CreatePlannedTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 700m, account.Id, null, null, null, "Rent"), CancellationToken.None);
        var planned = Assert.IsType<PlannedTransactionResponse>(Assert.IsType<CreatedResult>(plannedResult.Result).Value);

        var controller = new TransactionsController(db, new CurrentUserContext { FamilyId = family.Id });
        var result = await controller.Create(
            new CreateTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 700m, account.Id, null, null, planned.Id, "Rent paid"),
            CancellationToken.None);

        var created = Assert.IsType<TransactionResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(planned.Id, created.PlannedTransactionId);
        Assert.Equal(defaultPlan.Id, planned.PlanId);
    }

    [Fact]
    public async Task Create_MatchedToPlannedTransactionFromNonDefaultPlan_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var otherPlan = await TestDb.AddPlanAsync(db, family.Id, "New car");
        var planning = new PlanningController(db, new CurrentUserContext { FamilyId = family.Id });
        var plannedResult = await planning.CreatePlanned(new CreatePlannedTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 700m, account.Id, null, null, null, "Extra car cost", otherPlan.Id), CancellationToken.None);
        var planned = Assert.IsType<PlannedTransactionResponse>(Assert.IsType<CreatedResult>(plannedResult.Result).Value);

        var controller = new TransactionsController(db, new CurrentUserContext { FamilyId = family.Id });
        var result = await controller.Create(
            new CreateTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 700m, account.Id, null, null, planned.Id, "Should not match"),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("The selected planned transaction does not belong to the family's default plan.", badRequest.Value);
        Assert.Empty(db.ActualTransactions);
    }

    [Fact]
    public async Task Create_MatchedToPlannedTransactionFromAnotherFamily_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (familyA, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family A");
        var accountA = await TestDb.AddAccountAsync(db, familyA.Id);
        var (familyB, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family B");
        var accountB = await TestDb.AddAccountAsync(db, familyB.Id);
        var planningB = new PlanningController(db, new CurrentUserContext { FamilyId = familyB.Id });
        var plannedResult = await planningB.CreatePlanned(new CreatePlannedTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 700m, accountB.Id, null, null, null, "Family B rent"), CancellationToken.None);
        var plannedB = Assert.IsType<PlannedTransactionResponse>(Assert.IsType<CreatedResult>(plannedResult.Result).Value);

        var controllerA = new TransactionsController(db, new CurrentUserContext { FamilyId = familyA.Id });
        var result = await controllerA.Create(
            new CreateTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 700m, accountA.Id, null, null, plannedB.Id, "Cross-tenant match attempt"),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("The selected planned transaction does not belong to the family's default plan.", badRequest.Value);
        Assert.Empty(db.ActualTransactions);
    }

    [Fact]
    public async Task Create_MatchedToPlannedTransactionAlreadyMatchedToAnotherActual_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var planning = new PlanningController(db, new CurrentUserContext { FamilyId = family.Id });
        var plannedResult = await planning.CreatePlanned(new CreatePlannedTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 700m, account.Id, null, null, null, "Rent"), CancellationToken.None);
        var planned = Assert.IsType<PlannedTransactionResponse>(Assert.IsType<CreatedResult>(plannedResult.Result).Value);

        var controller = new TransactionsController(db, new CurrentUserContext { FamilyId = family.Id });
        await controller.Create(new CreateTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 700m, account.Id, null, null, planned.Id, "First match"), CancellationToken.None);

        var secondResult = await controller.Create(
            new CreateTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 700m, account.Id, null, null, planned.Id, "Second match attempt"),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(secondResult.Result);
        Assert.Equal("The selected planned transaction is already matched to another actual transaction.", badRequest.Value);
        Assert.Single(db.ActualTransactions);
    }

    [Fact]
    public async Task Update_KeepingItsOwnExistingMatch_DoesNotTriggerAlreadyMatchedRejection()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var planning = new PlanningController(db, new CurrentUserContext { FamilyId = family.Id });
        var plannedResult = await planning.CreatePlanned(new CreatePlannedTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 700m, account.Id, null, null, null, "Rent"), CancellationToken.None);
        var planned = Assert.IsType<PlannedTransactionResponse>(Assert.IsType<CreatedResult>(plannedResult.Result).Value);

        var controller = new TransactionsController(db, new CurrentUserContext { FamilyId = family.Id });
        var createResult = await controller.Create(new CreateTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 700m, account.Id, null, null, planned.Id, "Rent paid"), CancellationToken.None);
        var created = Assert.IsType<TransactionResponse>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);

        var updateResult = await controller.Update(created.Id,
            new UpdateTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 705m, account.Id, null, null, planned.Id, "Rent paid (amount corrected)"),
            CancellationToken.None);

        var updated = Assert.IsType<TransactionResponse>(Assert.IsType<OkObjectResult>(updateResult.Result).Value);
        Assert.Equal(planned.Id, updated.PlannedTransactionId);
        Assert.Equal(705m, updated.Amount);
    }
}
