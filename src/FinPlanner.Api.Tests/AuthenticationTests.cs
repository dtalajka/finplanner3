using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinPlanner.Api.Contracts;
using FinPlanner.Api.Data;
using FinPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinPlanner.Api.Tests;

public class AuthenticationTests
{
    // Matches Program.cs's AddJsonOptions (JsonStringEnumConverter) — System.Net.Http.Json's default
    // ReadFromJsonAsync<T> doesn't know about it, so role/etc. enums serialized as strings would otherwise fail.
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

    private static RegisterRequest MakeRegisterRequest(string? email = null, string familyName = "Test Family", string name = "Owner", string password = "correct-horse-battery") =>
        new(familyName, name, email ?? $"{Guid.NewGuid():N}@example.com", password);

    [Fact]
    public async Task Register_CreatesFamilyAndOwner_AndSignsInImmediately()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(name: "Dusan"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registered = await registerResponse.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        Assert.NotNull(registered);
        Assert.Equal(UserRole.Owner, registered!.Role);

        // Same HttpClient (same cookie jar) — registration must have signed the new Owner in immediately.
        var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var me = await meResponse.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        Assert.Equal(registered.Id, me!.Id);
        Assert.Equal(registered.FamilyId, me.FamilyId);
    }

    [Fact]
    public async Task Register_AlsoCreatesADefaultPlan_ForecastDoesNotFail()
    {
        // Registration must create the family's default Plan, or every existing Forecast/ATS/Compliance/
        // Planning endpoint would 500 for a brand-new family (Part B decision #2/#4) — verified end-to-end.
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest());

        var forecastResponse = await client.GetAsync("/api/planning/forecast");

        Assert.Equal(HttpStatusCode.OK, forecastResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_Succeeds()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var registerClient = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        await registerClient.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(email: email, password: "correct-horse-battery"));

        using var loginClient = factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "correct-horse-battery"));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var meResponse = await loginClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_And_UnknownEmail_ReturnTheSameGenericMessage()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var registerClient = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        await registerClient.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(email: email, password: "correct-horse-battery"));

        using var client = factory.CreateClient();
        var wrongPasswordResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "wrong-password"));
        var unknownEmailResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nobody@example.com", "whatever123"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmailResponse.StatusCode);
        var wrongPasswordBody = await wrongPasswordResponse.Content.ReadAsStringAsync();
        var unknownEmailBody = await unknownEmailResponse.Content.ReadAsStringAsync();
        Assert.Equal(unknownEmailBody, wrongPasswordBody); // identical — no email-enumeration signal
    }

    [Fact]
    public async Task Register_DuplicateEmail_IsRejected()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(email: email));

        using var secondClient = factory.CreateClient();
        var secondResponse = await secondClient.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(email: email, familyName: "Another Family"));

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Register_EmailUniqueness_IsCaseInsensitive()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(email: "Dusan@Example.com"));

        using var secondClient = factory.CreateClient();
        var secondResponse = await secondClient.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(email: "dusan@example.com", familyName: "Another Family"));

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Login_EmailIsCaseInsensitive()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var registerClient = factory.CreateClient();
        await registerClient.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(email: "Dusan@Example.com", password: "correct-horse-battery"));

        using var loginClient = factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/login", new LoginRequest("DUSAN@example.COM", "correct-horse-battery"));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Register_PasswordTooShort_IsRejected()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(password: "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutAuthentication_ReturnsUnauthorized()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutAuthentication_ReturnsUnauthorized()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ClearsCookie_SubsequentRequestIsUnauthorized()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        var logoutResponse = await client.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var afterLogout = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task FamilyIsolation_TwoRegisteredFamilies_NeverSeeEachOthersAccounts()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();
        await clientA.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(familyName: "Family A"));
        await clientB.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(familyName: "Family B"));

        await clientA.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Family A account"));
        await clientB.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Family B account"));

        var accountsForA = await (await clientA.GetAsync("/api/accounts")).Content.ReadFromJsonAsync<List<AccountResponse>>(JsonOptions);
        var accountsForB = await (await clientB.GetAsync("/api/accounts")).Content.ReadFromJsonAsync<List<AccountResponse>>(JsonOptions);

        Assert.Single(accountsForA!);
        Assert.Equal("Family A account", accountsForA![0].Name);
        Assert.Single(accountsForB!);
        Assert.Equal("Family B account", accountsForB![0].Name);
    }

    [Fact]
    public async Task CrossFamilyAccessAttempt_PlanIdFromAnotherFamily_IsRejected()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();
        var registeredB = await (await clientB.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(familyName: "Family B"))).Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        await clientA.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(familyName: "Family A"));

        var plansForB = await (await clientB.GetAsync("/api/plans")).Content.ReadFromJsonAsync<List<PlanResponse>>(JsonOptions);
        var familyBPlanId = plansForB!.Single().Id;

        // Family A, authenticated, tries to reach Family B's plan by putting its id directly in the query string.
        var response = await clientA.GetAsync($"/api/planning/forecast?planId={familyBPlanId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(registeredB);
    }

    [Fact]
    public async Task GetUsers_OnlyReturnsSameFamilyUsers()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();
        await clientA.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(familyName: "Family A", name: "A-Owner"));
        await clientB.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(familyName: "Family B", name: "B-Owner"));

        var usersForA = await (await clientA.GetAsync("/api/users")).Content.ReadFromJsonAsync<List<UserResponse>>(JsonOptions);

        Assert.Single(usersForA!);
        Assert.Equal("A-Owner", usersForA![0].Name);
    }

    [Fact]
    public async Task AddFamilyMember_AsOwner_Succeeds_AndNewMemberCanLogIn()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var ownerClient = factory.CreateClient();
        await ownerClient.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(name: "Owner"));
        var memberEmail = $"{Guid.NewGuid():N}@example.com";

        var addResponse = await ownerClient.PostAsJsonAsync("/api/users", new CreateFamilyMemberRequest("Member", memberEmail, "member-password-1", UserRole.Member));
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);

        using var memberClient = factory.CreateClient();
        var loginResponse = await memberClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(memberEmail, "member-password-1"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var usersForFamily = await (await memberClient.GetAsync("/api/users")).Content.ReadFromJsonAsync<List<UserResponse>>(JsonOptions);
        Assert.Equal(2, usersForFamily!.Count); // same family as the Owner who created them
    }

    [Fact]
    public async Task AddFamilyMember_AsNonOwnerMember_IsForbidden()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var ownerClient = factory.CreateClient();
        await ownerClient.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(name: "Owner"));
        var memberEmail = $"{Guid.NewGuid():N}@example.com";
        await ownerClient.PostAsJsonAsync("/api/users", new CreateFamilyMemberRequest("Member", memberEmail, "member-password-1", UserRole.Member));

        using var memberClient = factory.CreateClient();
        await memberClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(memberEmail, "member-password-1"));

        var response = await memberClient.PostAsJsonAsync("/api/users", new CreateFamilyMemberRequest("Intruder", $"{Guid.NewGuid():N}@example.com", "whatever123", UserRole.Member));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LegacyXUserIdHeader_IsRejected_OnlyTheAuthenticationCookieGrantsAccess()
    {
        // Part M decision #6: the dual-mode fallback has been removed — this is the regression test proving
        // the pre-authentication hole (anyone who knew/guessed a numeric user id could impersonate them) is
        // actually closed, not just documented as closed.
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient();
        var registered = await (await client.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest())).Content.ReadFromJsonAsync<UserResponse>(JsonOptions);

        using var anonymousClient = factory.CreateClient();
        anonymousClient.DefaultRequestHeaders.Add("X-User-Id", registered!.Id.ToString());
        var response = await anonymousClient.GetAsync("/api/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PasswordIsNeverStoredAsPlaintext()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateClient();
        const string plainPassword = "correct-horse-battery";
        var registered = await (await client.PostAsJsonAsync("/api/auth/register", MakeRegisterRequest(password: plainPassword))).Content.ReadFromJsonAsync<UserResponse>(JsonOptions);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinPlannerDbContext>();
        var storedUser = await dbContext.Users.SingleAsync(user => user.Id == registered!.Id);

        Assert.NotEqual(plainPassword, storedUser.PasswordHash);
        Assert.True(storedUser.PasswordHash.Length > plainPassword.Length); // PBKDF2 output is a long encoded blob, not the raw password
    }
}
