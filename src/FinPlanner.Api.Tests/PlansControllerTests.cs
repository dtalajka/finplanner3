using FinPlanner.Api.Contracts;
using FinPlanner.Api.Controllers;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FinPlanner.Api.Tests;

public class PlansControllerTests
{
    [Fact]
    public async Task SetDefault_SwitchesDefaultAndUnsetsPrevious()
    {
        await using var db = TestDb.CreateContext();
        var (family, originalDefault) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var controller = new PlansController(db, new CurrentUserContext { FamilyId = family.Id });

        var created = await controller.Create(new CreatePlanRequest("New car", null), CancellationToken.None);
        var newPlan = Assert.IsType<PlanResponse>(Assert.IsType<CreatedResult>(created.Result).Value);
        Assert.False(newPlan.IsDefault);

        var setDefault = await controller.SetDefault(newPlan.Id, CancellationToken.None);
        var updated = Assert.IsType<PlanResponse>(Assert.IsType<OkObjectResult>(setDefault.Result).Value);
        Assert.True(updated.IsDefault);

        var list = await controller.List(CancellationToken.None);
        var plans = Assert.IsType<List<PlanResponse>>(Assert.IsType<OkObjectResult>(list.Result).Value);
        Assert.Single(plans, plan => plan.IsDefault);
        Assert.True(plans.Single(plan => plan.Id == newPlan.Id).IsDefault);
        Assert.False(plans.Single(plan => plan.Id == originalDefault.Id).IsDefault);
    }

    [Fact]
    public async Task SetDefault_UnknownOrOtherFamilyPlan_ReturnsNotFound()
    {
        await using var db = TestDb.CreateContext();
        var (familyA, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family A");
        var (_, familyBDefault) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family B");

        var controller = new PlansController(db, new CurrentUserContext { FamilyId = familyA.Id });

        var result = await controller.SetDefault(familyBDefault.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task List_OnlyReturnsPlansForTheCurrentFamily()
    {
        await using var db = TestDb.CreateContext();
        var (familyA, planA) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family A");
        await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family B");

        var controller = new PlansController(db, new CurrentUserContext { FamilyId = familyA.Id });
        var result = await controller.List(CancellationToken.None);

        var plans = Assert.IsType<List<PlanResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Single(plans);
        Assert.Equal(planA.Id, plans[0].Id);
    }

    [Fact]
    public async Task List_WithoutCurrentUser_ReturnsUnauthorized()
    {
        await using var db = TestDb.CreateContext();
        var controller = new PlansController(db, new CurrentUserContext());

        var result = await controller.List(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task SetReserve_UpdatesMinimumReserve_AndNullClearsIt()
    {
        await using var db = TestDb.CreateContext();
        var (family, plan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var controller = new PlansController(db, new CurrentUserContext { FamilyId = family.Id });

        var setResult = await controller.SetReserve(plan.Id, new SetPlanReserveRequest(750m), CancellationToken.None);
        var response = Assert.IsType<PlanResponse>(Assert.IsType<OkObjectResult>(setResult.Result).Value);
        Assert.Equal(750m, response.MinimumReserve);

        var clearResult = await controller.SetReserve(plan.Id, new SetPlanReserveRequest(null), CancellationToken.None);
        var cleared = Assert.IsType<PlanResponse>(Assert.IsType<OkObjectResult>(clearResult.Result).Value);
        Assert.Null(cleared.MinimumReserve);
    }

    [Fact]
    public async Task SetReserve_NegativeValue_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (family, plan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var controller = new PlansController(db, new CurrentUserContext { FamilyId = family.Id });

        var result = await controller.SetReserve(plan.Id, new SetPlanReserveRequest(-1m), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SetReserve_PlanFromAnotherFamily_ReturnsNotFound()
    {
        await using var db = TestDb.CreateContext();
        var (familyA, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family A");
        var (_, familyBPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family B");
        var controller = new PlansController(db, new CurrentUserContext { FamilyId = familyA.Id });

        var result = await controller.SetReserve(familyBPlan.Id, new SetPlanReserveRequest(100m), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
