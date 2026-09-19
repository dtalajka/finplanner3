using FinPlanner.Api.Contracts;
using FinPlanner.Api.Controllers;
using FinPlanner.Api.Domain;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FinPlanner.Api.Tests;

public class ForecastControllerTests
{
    [Fact]
    public async Task Forecast_UsesFamilyDefaultPlan_WhenPlanIdOmitted()
    {
        await using var db = TestDb.CreateContext();
        var (family, defaultPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var currentUser = new CurrentUserContext { FamilyId = family.Id };
        var planningController = new PlanningController(db, currentUser);
        await planningController.CreatePlanned(new CreatePlannedTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5), 100m, null, account.Id, null, null, "Default plan income"), CancellationToken.None);

        var forecastController = new ForecastController(db, currentUser);
        var result = await forecastController.Get(null, null, CancellationToken.None);

        var response = Assert.IsType<ForecastResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(defaultPlan.Id, response.PlanId);
        Assert.Single(response.Events);
        Assert.Equal("Default plan income", response.Events[0].Description);
    }

    [Fact]
    public async Task Forecast_ForPlanA_NeverIncludesPlanBsRecurringPlannedOrGoalData()
    {
        await using var db = TestDb.CreateContext();
        var (family, planA) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var planB = await TestDb.AddPlanAsync(db, family.Id, "New car");
        var currentUser = new CurrentUserContext { FamilyId = family.Id };
        var planningController = new PlanningController(db, currentUser);

        await planningController.CreatePlanned(new CreatePlannedTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3), 50m, account.Id, null, null, null, "Plan A item", planA.Id), CancellationToken.None);
        await planningController.CreatePlanned(new CreatePlannedTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3), 999m, account.Id, null, null, null, "Plan B item", planB.Id), CancellationToken.None);
        await planningController.CreateRecurring(new CreateRecurringRuleRequest("Plan B rule", 500m, account.Id, null, null, RecurrenceFrequency.Monthly, 1, DateOnly.FromDateTime(DateTime.UtcNow), null, null, planB.Id), CancellationToken.None);

        var forecastController = new ForecastController(db, currentUser);
        var result = await forecastController.Get(planA.Id, null, CancellationToken.None);

        var response = Assert.IsType<ForecastResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.All(response.Events, e => Assert.NotEqual(999m, e.Amount));
        Assert.All(response.Events, e => Assert.NotEqual(500m, e.Amount));
        Assert.Contains(response.Events, e => e.Description == "Plan A item");
    }

    [Fact]
    public async Task Forecast_IncludesAllFamilyActuals_RegardlessOfPlan()
    {
        await using var db = TestDb.CreateContext();
        var (family, planA) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var currentUser = new CurrentUserContext { FamilyId = family.Id };
        var transactionsController = new TransactionsController(db, currentUser);
        // Actuals are family-global and were recorded before "today" — they must be reflected in currentBalance
        // regardless of which plan the forecast is requested for.
        await transactionsController.Create(new CreateTransactionRequest(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5), 700m, null, account.Id, null, null, "Salary"), CancellationToken.None);

        var forecastController = new ForecastController(db, currentUser);
        var result = await forecastController.Get(planA.Id, null, CancellationToken.None);

        var response = Assert.IsType<ForecastResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(700m, response.Accounts.Single().CurrentBalance);
    }

    [Fact]
    public async Task Forecast_WithPlanIdFromAnotherFamily_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (familyA, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family A");
        var (_, familyBPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family B");

        var forecastController = new ForecastController(db, new CurrentUserContext { FamilyId = familyA.Id });
        var result = await forecastController.Get(familyBPlan.Id, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task HorizonMonths_IsClampedToSixtyMonths()
    {
        // Part O: the cap was raised from 36 to 60 months to support a 5-year horizon selector in the UI.
        await using var db = TestDb.CreateContext();
        var (family, planA) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        var currentUser = new CurrentUserContext { FamilyId = family.Id };
        var planningController = new PlanningController(db, currentUser);
        await planningController.CreateRecurring(new CreateRecurringRuleRequest("Salary", 100m, null, account.Id, null, RecurrenceFrequency.Monthly, 1, DateOnly.FromDateTime(DateTime.UtcNow), null, null), CancellationToken.None);

        var forecastController = new ForecastController(db, currentUser);
        var result = await forecastController.Get(planA.Id, 1000, CancellationToken.None);

        var response = Assert.IsType<ForecastResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var expectedHorizonEnd = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(60);
        Assert.Equal(expectedHorizonEnd, response.HorizonEnd);
        Assert.All(response.Events, e => Assert.True(e.Date <= expectedHorizonEnd));
    }

    [Fact]
    public async Task HorizonMonths_BelowMinimum_IsClampedToOne()
    {
        await using var db = TestDb.CreateContext();
        var (family, planA) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var forecastController = new ForecastController(db, new CurrentUserContext { FamilyId = family.Id });

        var result = await forecastController.Get(planA.Id, 0, CancellationToken.None);

        var response = Assert.IsType<ForecastResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(1), response.HorizonEnd);
    }

    [Fact]
    public async Task AvailableToSpend_UsesFamilyDefaultReserve_WhenPlanReserveIsNull()
    {
        await using var db = TestDb.CreateContext();
        var (family, planA) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        account.OpeningBalance = 300m;
        var familyController = new FamilyController(db, new CurrentUserContext { FamilyId = family.Id });
        await familyController.Update(new UpdateFamilyReserveRequest(200m), CancellationToken.None);
        await db.SaveChangesAsync();

        var forecastController = new ForecastController(db, new CurrentUserContext { FamilyId = family.Id });
        var result = await forecastController.GetAvailableToSpend(planA.Id, null, CancellationToken.None);

        var response = Assert.IsType<AvailableToSpendResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(200m, response.MinimumReserve);
        Assert.Equal(100m, response.AvailableToSpend);
    }

    [Fact]
    public async Task AvailableToSpend_PlanOwnReserve_OverridesFamilyDefault()
    {
        await using var db = TestDb.CreateContext();
        var (family, planA) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        account.OpeningBalance = 300m;
        var familyController = new FamilyController(db, new CurrentUserContext { FamilyId = family.Id });
        await familyController.Update(new UpdateFamilyReserveRequest(200m), CancellationToken.None);
        var plansController = new PlansController(db, new CurrentUserContext { FamilyId = family.Id });
        await plansController.SetReserve(planA.Id, new SetPlanReserveRequest(50m), CancellationToken.None);
        await db.SaveChangesAsync();

        var forecastController = new ForecastController(db, new CurrentUserContext { FamilyId = family.Id });
        var result = await forecastController.GetAvailableToSpend(planA.Id, null, CancellationToken.None);

        var response = Assert.IsType<AvailableToSpendResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(50m, response.MinimumReserve); // plan override wins over the family default of 200
        Assert.Equal(250m, response.AvailableToSpend);
    }

    [Fact]
    public async Task AvailableToSpend_ForPlanA_NeverIncludesPlanBsGoals()
    {
        await using var db = TestDb.CreateContext();
        var (family, planA) = await TestDb.SeedFamilyWithDefaultPlanAsync(db);
        var account = await TestDb.AddAccountAsync(db, family.Id);
        account.OpeningBalance = 1000m;
        await db.SaveChangesAsync();
        var planB = await TestDb.AddPlanAsync(db, family.Id, "New car");
        await TestDb.AddGoalAsync(db, family.Id, planB.Id, "Plan B goal", targetAmount: 5000m);

        var forecastController = new ForecastController(db, new CurrentUserContext { FamilyId = family.Id });
        var result = await forecastController.GetAvailableToSpend(planA.Id, null, CancellationToken.None);

        var response = Assert.IsType<AvailableToSpendResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Empty(response.Goals); // Plan B's goal must not appear in Plan A's feasibility report
        Assert.Equal(1000m, response.AvailableToSpend);
    }

    [Fact]
    public async Task AvailableToSpend_WithPlanIdFromAnotherFamily_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (familyA, _) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family A");
        var (_, familyBPlan) = await TestDb.SeedFamilyWithDefaultPlanAsync(db, "Family B");

        var forecastController = new ForecastController(db, new CurrentUserContext { FamilyId = familyA.Id });
        var result = await forecastController.GetAvailableToSpend(familyBPlan.Id, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
