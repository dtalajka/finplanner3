using FinPlanner.Api.Contracts;
using FinPlanner.Api.Controllers;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FinPlanner.Api.Tests;

public class FamilyControllerTests
{
    [Fact]
    public async Task Get_DefaultsToZeroReserve()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var controller = new FamilyController(db, new CurrentUserContext { FamilyId = family.Id });

        var result = await controller.Get(CancellationToken.None);

        var response = Assert.IsType<FamilyResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(0m, response.DefaultMinimumReserve);
    }

    [Fact]
    public async Task Update_SetsDefaultMinimumReserve()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var controller = new FamilyController(db, new CurrentUserContext { FamilyId = family.Id });

        var updateResult = await controller.Update(new UpdateFamilyReserveRequest(1500m), CancellationToken.None);
        var updated = Assert.IsType<FamilyResponse>(Assert.IsType<OkObjectResult>(updateResult.Result).Value);
        Assert.Equal(1500m, updated.DefaultMinimumReserve);

        var getResult = await controller.Get(CancellationToken.None);
        var fetched = Assert.IsType<FamilyResponse>(Assert.IsType<OkObjectResult>(getResult.Result).Value);
        Assert.Equal(1500m, fetched.DefaultMinimumReserve);
    }

    [Fact]
    public async Task Update_NegativeValue_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var controller = new FamilyController(db, new CurrentUserContext { FamilyId = family.Id });

        var result = await controller.Update(new UpdateFamilyReserveRequest(-5m), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Get_WithoutCurrentUser_ReturnsUnauthorized()
    {
        await using var db = TestDb.CreateContext();
        var controller = new FamilyController(db, new CurrentUserContext());

        var result = await controller.Get(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }
}
