using FinPlanner.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinPlanner.Api.Tests;

// First use of a real HTTP-pipeline test host in this project (Part M) — every other test constructs a
// controller directly against a CurrentUserContext, which cannot exercise cookie issuance/validation or the
// authentication middleware itself. Each instance gets its own isolated InMemory database (same "fresh DB per
// test" philosophy as TestDb.CreateContext) and runs with ASPNETCORE_ENVIRONMENT=Testing so Program.cs skips
// SeedData (tests register their own users instead of relying on legacy seed data).
public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Swap only what DbContextOptions<FinPlannerDbContext> resolves to — building it directly via
            // DbContextOptionsBuilder (not through AddDbContext again) avoids EF registering the InMemory
            // provider's services into the same outer container Npgsql's AddDbContext already populated,
            // which otherwise throws "multiple database providers registered".
            services.RemoveAll<DbContextOptions<FinPlannerDbContext>>();
            services.AddSingleton(new DbContextOptionsBuilder<FinPlannerDbContext>().UseInMemoryDatabase(_databaseName).Options);
        });
    }
}
