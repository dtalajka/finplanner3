using FinPlanner.Api.Contracts;
using FinPlanner.Api.Controllers;
using FinPlanner.Api.Domain;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FinPlanner.Api.Tests;

public class SettlementControllerTests
{
    private static async Task<Category> AddCategoryAsync(Data.FinPlannerDbContext db, long familyId, string name = "Household", CategoryType type = CategoryType.Expense)
    {
        var category = new Category { FamilyId = familyId, Name = name, Type = type, Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    [Fact]
    public async Task Get_EndToEnd_WithFixedPercentageRule_ComputesSettlementFromRealActuals()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var userA = await TestDb.AddUserAsync(db, family.Id, "A");
        var userB = await TestDb.AddUserAsync(db, family.Id, "B");
        var joint = await TestDb.AddAccountAsync(db, family.Id, "Joint", ownerUserId: null);
        var category = await AddCategoryAsync(db, family.Id, "Mortgage");
        var currentUser = new CurrentUserContext { FamilyId = family.Id };

        var rulesController = new AllocationRulesController(db, currentUser);
        await rulesController.Create(new CreateAllocationRuleRequest(category.Id, ExpenseAllocationMethod.FixedPercentage,
            [new AllocationShareRequest(userA.Id, 40m, null), new AllocationShareRequest(userB.Id, 60m, null)]), CancellationToken.None);

        var transactions = new TransactionsController(db, currentUser);
        await transactions.Create(new CreateTransactionRequest(new DateOnly(2026, 7, 15), 1200m, joint.Id, null, category.Id, null, "Mortgage"), CancellationToken.None);

        var controller = new SettlementController(db, currentUser);
        var result = await controller.Get(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), CancellationToken.None);

        var response = Assert.IsType<SettlementResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var a = response.Users.Single(u => u.UserId == userA.Id);
        var b = response.Users.Single(u => u.UserId == userB.Id);
        Assert.Equal(480m, a.TotalExpectedContribution);
        Assert.Equal(720m, b.TotalExpectedContribution);
        Assert.Equal(1200m, response.PoolContribution);
        Assert.Equal(0m, response.UnattributedAmount);
    }

    [Fact]
    public async Task Get_DefaultRange_IsCurrentCalendarMonth()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var controller = new SettlementController(db, new CurrentUserContext { FamilyId = family.Id });
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await controller.Get(null, null, CancellationToken.None);

        var response = Assert.IsType<SettlementResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(new DateOnly(today.Year, today.Month, 1), response.From);
        Assert.Equal(new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)), response.To);
    }

    [Fact]
    public async Task Get_UnallocatedPooledExpense_SurfacesAsUnattributedAmount_NotSilentlyDropped()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var joint = await TestDb.AddAccountAsync(db, family.Id, "Joint", ownerUserId: null);
        var category = await AddCategoryAsync(db, family.Id, "Misc"); // no rule for it
        var currentUser = new CurrentUserContext { FamilyId = family.Id };
        var transactions = new TransactionsController(db, currentUser);
        await transactions.Create(new CreateTransactionRequest(new DateOnly(2026, 7, 5), 42m, joint.Id, null, category.Id, null, "Unplanned misc"), CancellationToken.None);

        var controller = new SettlementController(db, currentUser);
        var result = await controller.Get(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), CancellationToken.None);

        var response = Assert.IsType<SettlementResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(42m, response.UnattributedAmount);
        Assert.Equal(0m, response.PoolContribution);
    }

    [Fact]
    public async Task Get_CrossFamilyIsolation_NeverLeaksOtherFamilysExpensesOrRules()
    {
        await using var db = TestDb.CreateContext();
        var (familyA, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family A");
        var (familyB, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family B");
        var userB = await TestDb.AddUserAsync(db, familyB.Id, "B");
        var jointB = await TestDb.AddAccountAsync(db, familyB.Id, "Joint B", ownerUserId: null);
        var categoryB = await AddCategoryAsync(db, familyB.Id);
        var currentUserB = new CurrentUserContext { FamilyId = familyB.Id };
        await new AllocationRulesController(db, currentUserB).Create(new CreateAllocationRuleRequest(categoryB.Id, ExpenseAllocationMethod.Equal, [new AllocationShareRequest(userB.Id, null, null)]), CancellationToken.None);
        await new TransactionsController(db, currentUserB).Create(new CreateTransactionRequest(new DateOnly(2026, 7, 5), 500m, jointB.Id, null, categoryB.Id, null, "Family B expense"), CancellationToken.None);

        var controllerA = new SettlementController(db, new CurrentUserContext { FamilyId = familyA.Id });
        var result = await controllerA.Get(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), CancellationToken.None);

        var response = Assert.IsType<SettlementResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Empty(response.Users);
        Assert.Empty(response.Categories);
        Assert.Equal(0m, response.PoolContribution);
    }

    [Fact]
    public async Task Get_FromAfterTo_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (family, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var controller = new SettlementController(db, new CurrentUserContext { FamilyId = family.Id });

        var result = await controller.Get(new DateOnly(2026, 8, 1), new DateOnly(2026, 7, 1), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Get_WithoutCurrentUser_ReturnsUnauthorized()
    {
        await using var db = TestDb.CreateContext();
        var controller = new SettlementController(db, new CurrentUserContext());

        var result = await controller.Get(null, null, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }
}
