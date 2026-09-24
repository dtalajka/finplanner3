using FinPlanner.Api.Data;
using FinPlanner.Api.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
    policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials())); // required so the browser sends/accepts the auth cookie cross-origin (Part M)
builder.Services.AddDbContext<FinPlannerDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "finplanner")));
builder.Services.AddScoped<CurrentUserContext>();

// Part M: cookie-based session authentication. No [Authorize] attributes are used anywhere in this codebase —
// every controller keeps using the existing manual TenantControllerBase.RequireFamily pattern; this only
// changes what populates HttpContext.User, which TenantResolutionMiddleware then reads. The redirect-to-login
// events are overridden because this is a JSON API, not an MVC app with login pages — without this, a
// Challenge/Forbid would produce an HTML redirect instead of a plain 401/403.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "finplanner.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // SameAsRequest in every environment: the cookie is Secure over HTTPS and plain over HTTP, so the app
        // also works when deployed over plain HTTP (a Secure cookie is silently refused on a non-HTTPS request).
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// In the container image the built React app lives in wwwroot and is served from the same origin as the API.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("frontend");

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();

// Demo family with a known password — only ever seeded in Development, never in a deployed environment.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<FinPlannerDbContext>();
        await SeedData.EnsureSeedDataAsync(dbContext);
    }
    catch (Exception exception)
    {
        app.Logger.LogWarning(exception, "Could not seed initial data. Is the database reachable and migrated?");
    }
}

app.MapGet("/api/health/database", async (FinPlannerDbContext dbContext, CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
    return canConnect
        ? Results.Ok(new { status = "connected" })
        : Results.Problem("The configured PostgreSQL database is not reachable.", statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.Run();

// Exposes the implicit top-level Program class to WebApplicationFactory<Program> in the test project.
public partial class Program;
