using FinPlanner.Api.Contracts;
using FinPlanner.Api.Controllers;
using FinPlanner.Api.Domain;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FinPlanner.Api.Tests;

public class AllocationRulesControllerTests
{
    private static async Task<Category> AddCategoryAsync(Data.FinPlannerDbContext db, long familyId, string name = "Household", CategoryType type = CategoryType.Expense)
    {
        var category = new Category { FamilyId = familyId, Name = name, Type = type, Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    [Fact]
    public async Task Create_FixedPercentage_PercentagesSumTo100_Succeeds()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var userA = await TestDb.AddUserAsync(db, family.Id, "A");
        var userB = await TestDb.AddUserAsync(db, family.Id, "B");
        var category = await AddCategoryAsync(db, family.Id);
        var controller = new AllocationRulesController(db, new CurrentUserContext { FamilyId = family.Id });

        var result = await controller.Create(new CreateAllocationRuleRequest(category.Id, ExpenseAllocationMethod.FixedPercentage,
            [new AllocationShareRequest(userA.Id, 40m, null), new AllocationShareRequest(userB.Id, 60m, null)]), CancellationToken.None);

        var created = Assert.IsType<AllocationRuleResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(category.Id, created.CategoryId);
        Assert.Equal(2, created.Shares.Count);
    }

    [Fact]
    public async Task Create_FixedPercentage_NotSummingTo100_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var userA = await TestDb.AddUserAsync(db, family.Id, "A");
        var category = await AddCategoryAsync(db, family.Id);
        var controller = new AllocationRulesController(db, new CurrentUserContext { FamilyId = family.Id });

        var result = await controller.Create(new CreateAllocationRuleRequest(category.Id, ExpenseAllocationMethod.FixedPercentage,
            [new AllocationShareRequest(userA.Id, 90m, null)]), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Percentages must sum to exactly 100.", badRequest.Value);
    }

    [Fact]
    public async Task Create_FixedAmount_MissingAmountOnAShare_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var userA = await TestDb.AddUserAsync(db, family.Id, "A");
        var category = await AddCategoryAsync(db, family.Id);
        var controller = new AllocationRulesController(db, new CurrentUserContext { FamilyId = family.Id });

        var result = await controller.Create(new CreateAllocationRuleRequest(category.Id, ExpenseAllocationMethod.FixedAmount,
            [new AllocationShareRequest(userA.Id, null, null)]), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Every share needs a fixed amount for the FixedAmount method.", badRequest.Value);
    }

    [Fact]
    public async Task Create_SecondActiveRuleForSameCategory_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var userA = await TestDb.AddUserAsync(db, family.Id, "A");
        var category = await AddCategoryAsync(db, family.Id);
        var controller = new AllocationRulesController(db, new CurrentUserContext { FamilyId = family.Id });
        await controller.Create(new CreateAllocationRuleRequest(category.Id, ExpenseAllocationMethod.Equal, [new AllocationShareRequest(userA.Id, null, null)]), CancellationToken.None);

        var second = await controller.Create(new CreateAllocationRuleRequest(category.Id, ExpenseAllocationMethod.Equal, [new AllocationShareRequest(userA.Id, null, null)]), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(second.Result);
        Assert.Equal("An active allocation rule already exists for this category.", badRequest.Value);
    }

    [Fact]
    public async Task Create_SecondActiveFamilyDefaultRule_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var userA = await TestDb.AddUserAsync(db, family.Id, "A");
        var controller = new AllocationRulesController(db, new CurrentUserContext { FamilyId = family.Id });
        await controller.Create(new CreateAllocationRuleRequest(null, ExpenseAllocationMethod.Equal, [new AllocationShareRequest(userA.Id, null, null)]), CancellationToken.None);

        var second = await controller.Create(new CreateAllocationRuleRequest(null, ExpenseAllocationMethod.Equal, [new AllocationShareRequest(userA.Id, null, null)]), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(second.Result);
        Assert.Equal("An active family-default allocation rule already exists.", badRequest.Value);
    }

    [Fact]
    public async Task Create_WithUserFromAnotherFamily_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (familyA, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family A");
        var (familyB, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family B");
        var userB = await TestDb.AddUserAsync(db, familyB.Id, "B");
        var category = await AddCategoryAsync(db, familyA.Id);
        var controller = new AllocationRulesController(db, new CurrentUserContext { FamilyId = familyA.Id });

        var result = await controller.Create(new CreateAllocationRuleRequest(category.Id, ExpenseAllocationMethod.Equal, [new AllocationShareRequest(userB.Id, null, null)]), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("All shares must reference users belonging to this family.", badRequest.Value);
    }

    [Fact]
    public async Task Create_WithCategoryFromAnotherFamily_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (familyA, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family A");
        var (familyB, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family B");
        var userA = await TestDb.AddUserAsync(db, familyA.Id, "A");
        var categoryB = await AddCategoryAsync(db, familyB.Id);
        var controller = new AllocationRulesController(db, new CurrentUserContext { FamilyId = familyA.Id });

        var result = await controller.Create(new CreateAllocationRuleRequest(categoryB.Id, ExpenseAllocationMethod.Equal, [new AllocationShareRequest(userA.Id, null, null)]), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("The selected category does not belong to this family.", badRequest.Value);
    }

    [Fact]
    public async Task Update_ReplacesShares()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var userA = await TestDb.AddUserAsync(db, family.Id, "A");
        var userB = await TestDb.AddUserAsync(db, family.Id, "B");
        var category = await AddCategoryAsync(db, family.Id);
        var controller = new AllocationRulesController(db, new CurrentUserContext { FamilyId = family.Id });
        var createResult = await controller.Create(new CreateAllocationRuleRequest(category.Id, ExpenseAllocationMethod.Equal, [new AllocationShareRequest(userA.Id, null, null)]), CancellationToken.None);
        var created = Assert.IsType<AllocationRuleResponse>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);

        var updateResult = await controller.Update(created.Id, new UpdateAllocationRuleRequest(category.Id, ExpenseAllocationMethod.FixedPercentage,
            [new AllocationShareRequest(userA.Id, 40m, null), new AllocationShareRequest(userB.Id, 60m, null)]), CancellationToken.None);

        var updated = Assert.IsType<AllocationRuleResponse>(Assert.IsType<OkObjectResult>(updateResult.Result).Value);
        Assert.Equal(ExpenseAllocationMethod.FixedPercentage, updated.Method);
        Assert.Equal(2, updated.Shares.Count);
    }

    [Fact]
    public async Task Delete_SoftDeactivates_ExcludedFromDefaultListing_AllowsNewRuleForSameCategory()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var userA = await TestDb.AddUserAsync(db, family.Id, "A");
        var category = await AddCategoryAsync(db, family.Id);
        var controller = new AllocationRulesController(db, new CurrentUserContext { FamilyId = family.Id });
        var createResult = await controller.Create(new CreateAllocationRuleRequest(category.Id, ExpenseAllocationMethod.Equal, [new AllocationShareRequest(userA.Id, null, null)]), CancellationToken.None);
        var created = Assert.IsType<AllocationRuleResponse>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);

        var deleteResult = await controller.Delete(created.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(deleteResult);

        var defaultList = await controller.List(false, CancellationToken.None);
        Assert.Empty(Assert.IsType<List<AllocationRuleResponse>>(Assert.IsType<OkObjectResult>(defaultList.Result).Value));

        // Deactivating frees up the category for a brand-new active rule.
        var recreateResult = await controller.Create(new CreateAllocationRuleRequest(category.Id, ExpenseAllocationMethod.Equal, [new AllocationShareRequest(userA.Id, null, null)]), CancellationToken.None);
        Assert.IsType<CreatedAtActionResult>(recreateResult.Result);
    }

    [Fact]
    public async Task List_CrossFamilyIsolation()
    {
        await using var db = TestDb.CreateContext();
        var (familyA, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family A");
        var (familyB, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family B");
        var userB = await TestDb.AddUserAsync(db, familyB.Id, "B");
        var categoryB = await AddCategoryAsync(db, familyB.Id);
        var controllerB = new AllocationRulesController(db, new CurrentUserContext { FamilyId = familyB.Id });
        await controllerB.Create(new CreateAllocationRuleRequest(categoryB.Id, ExpenseAllocationMethod.Equal, [new AllocationShareRequest(userB.Id, null, null)]), CancellationToken.None);

        var controllerA = new AllocationRulesController(db, new CurrentUserContext { FamilyId = familyA.Id });
        var result = await controllerA.List(true, CancellationToken.None);

        Assert.Empty(Assert.IsType<List<AllocationRuleResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value));
    }
}
