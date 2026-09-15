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
}
