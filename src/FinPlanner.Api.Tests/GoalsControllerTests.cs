using FinPlanner.Api.Contracts;
using FinPlanner.Api.Controllers;
using FinPlanner.Api.Domain;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FinPlanner.Api.Tests;

public class GoalsControllerTests
{
    [Fact]
    public async Task Create_WithoutPlanId_UsesFamilyDefaultPlan()
    {
        await using var db = TestDb.CreateContext();
        var (family, defaultPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var controller = new GoalsController(db, new CurrentUserContext { FamilyId = family.Id });

        var result = await controller.Create(
            new CreateGoalRequest("Vacation", 2400m, DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(8), GoalPriority.Soft, null, null),
            CancellationToken.None);

        var created = Assert.IsType<GoalResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(defaultPlan.Id, created.PlanId);
    }

    [Fact]
    public async Task Create_WithPlanIdFromAnotherFamily_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (familyA, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family A");
        var (_, familyBPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family B");
        var controller = new GoalsController(db, new CurrentUserContext { FamilyId = familyA.Id });

        var result = await controller.Create(
            new CreateGoalRequest("Vacation", 2400m, DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(8), GoalPriority.Soft, null, null, familyBPlan.Id),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("The selected plan does not belong to this family.", badRequest.Value);
        Assert.Empty(db.Goals);
    }

    [Fact]
    public async Task CurrentFunding_OnlyCountsActualTransactions_NotPlannedOnes()
    {
        await using var db = TestDb.CreateContext();
        var (family, defaultPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var goal = await TestDb.AddGoalAsync(db, family.Id, defaultPlan.Id, targetAmount: 1000m);

        var planningController = new PlanningController(db, new CurrentUserContext { FamilyId = family.Id });
        await planningController.CreatePlanned(new CreatePlannedTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 300m, account.Id, null, null, null, "Planned contribution", null, goal.Id), CancellationToken.None);

        var transactionsController = new TransactionsController(db, new CurrentUserContext { FamilyId = family.Id });
        await transactionsController.Create(new CreateTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 150m, account.Id, null, null, null, "Actual contribution", goal.Id), CancellationToken.None);
        await transactionsController.Create(new CreateTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 50m, account.Id, null, null, null, "Actual contribution 2", goal.Id), CancellationToken.None);

        var goalsController = new GoalsController(db, new CurrentUserContext { FamilyId = family.Id });
        var result = await goalsController.Get(goal.Id, CancellationToken.None);

        var response = Assert.IsType<GoalResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(200m, response.CurrentFunding);
        Assert.Equal(800m, response.RemainingAmount);
        Assert.Equal("ACTIVE", response.Status);
    }

    [Fact]
    public async Task RemainingAmount_FloorsAtZero_AndStatusIsComplete_WhenFullyFunded()
    {
        await using var db = TestDb.CreateContext();
        var (family, defaultPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var goal = await TestDb.AddGoalAsync(db, family.Id, defaultPlan.Id, targetAmount: 500m);

        var transactionsController = new TransactionsController(db, new CurrentUserContext { FamilyId = family.Id });
        await transactionsController.Create(new CreateTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 600m, account.Id, null, null, null, "Overfunded", goal.Id), CancellationToken.None);

        var goalsController = new GoalsController(db, new CurrentUserContext { FamilyId = family.Id });
        var result = await goalsController.Get(goal.Id, CancellationToken.None);
        var response = Assert.IsType<GoalResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal(0m, response.RemainingAmount);
        Assert.Equal("COMPLETE", response.Status);
        Assert.Equal(0m, response.RequiredMonthlyContribution);
    }

    [Fact]
    public async Task OverdueGoal_DoesNotDivideByZero_AndIsFlaggedOverdue()
    {
        await using var db = TestDb.CreateContext();
        var (family, defaultPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var pastDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-2);
        var goal = await TestDb.AddGoalAsync(db, family.Id, defaultPlan.Id, targetAmount: 300m, targetDate: pastDate);

        var goalsController = new GoalsController(db, new CurrentUserContext { FamilyId = family.Id });
        var result = await goalsController.Get(goal.Id, CancellationToken.None);
        var response = Assert.IsType<GoalResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal("OVERDUE", response.Status);
        Assert.Equal(300m, response.RequiredMonthlyContribution); // remaining_periods floored at 1
    }

    [Fact]
    public async Task Delete_SoftDeletes_AndDoesNotBlockOnTaggedActuals()
    {
        await using var db = TestDb.CreateContext();
        var (family, defaultPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var goal = await TestDb.AddGoalAsync(db, family.Id, defaultPlan.Id);

        var transactionsController = new TransactionsController(db, new CurrentUserContext { FamilyId = family.Id });
        await transactionsController.Create(new CreateTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow), 100m, account.Id, null, null, null, "Contribution", goal.Id), CancellationToken.None);

        var goalsController = new GoalsController(db, new CurrentUserContext { FamilyId = family.Id });
        var deleteResult = await goalsController.Delete(goal.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(deleteResult);

        var activeList = await goalsController.List(null, false, CancellationToken.None);
        var activeGoals = Assert.IsType<List<GoalResponse>>(Assert.IsType<OkObjectResult>(activeList.Result).Value);
        Assert.Empty(activeGoals);

        var allList = await goalsController.List(null, true, CancellationToken.None);
        var allGoals = Assert.IsType<List<GoalResponse>>(Assert.IsType<OkObjectResult>(allList.Result).Value);
        Assert.Single(allGoals);
        Assert.Equal(100m, allGoals[0].CurrentFunding);
    }
}
