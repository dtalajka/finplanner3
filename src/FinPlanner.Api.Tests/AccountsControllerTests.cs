using FinPlanner.Api.Contracts;
using FinPlanner.Api.Controllers;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FinPlanner.Api.Tests;

public class AccountsControllerTests
{
    [Fact]
    public async Task Create_WithOwnerFromSameFamily_Succeeds()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var user = await TestDb.AddUserAsync(db, family.Id, "A");
        var controller = new AccountsController(db, new CurrentUserContext { FamilyId = family.Id });

        var result = await controller.Create(new CreateAccountRequest("Salary A", "EUR", 0, user.Id), CancellationToken.None);

        var created = Assert.IsType<AccountResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(user.Id, created.OwnerUserId);
    }

    [Fact]
    public async Task Create_WithoutOwner_LeavesAccountPooled()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var controller = new AccountsController(db, new CurrentUserContext { FamilyId = family.Id });

        var result = await controller.Create(new CreateAccountRequest("Joint"), CancellationToken.None);

        var created = Assert.IsType<AccountResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Null(created.OwnerUserId);
    }

    [Fact]
    public async Task Create_WithOwnerFromAnotherFamily_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (familyA, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family A");
        var (familyB, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family B");
        var userB = await TestDb.AddUserAsync(db, familyB.Id, "B");
        var controller = new AccountsController(db, new CurrentUserContext { FamilyId = familyA.Id });

        var result = await controller.Create(new CreateAccountRequest("Suspicious", "EUR", 0, userB.Id), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("The selected owner does not belong to this family.", badRequest.Value);
    }

    [Fact]
    public async Task Update_ChangesOwner()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var userA = await TestDb.AddUserAsync(db, family.Id, "A");
        var userB = await TestDb.AddUserAsync(db, family.Id, "B");
        var controller = new AccountsController(db, new CurrentUserContext { FamilyId = family.Id });
        var createResult = await controller.Create(new CreateAccountRequest("Account", "EUR", 0, userA.Id), CancellationToken.None);
        var created = Assert.IsType<AccountResponse>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);

        var updateResult = await controller.Update(created.Id, new UpdateAccountRequest("Account", "EUR", 0, true, userB.Id), CancellationToken.None);

        var updated = Assert.IsType<AccountResponse>(Assert.IsType<OkObjectResult>(updateResult.Result).Value);
        Assert.Equal(userB.Id, updated.OwnerUserId);
    }
}
