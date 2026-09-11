using FinPlanner.Api.Contracts;
using FinPlanner.Api.Data;
using FinPlanner.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(FinPlannerDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> List(CancellationToken cancellationToken) => Ok(await dbContext.Categories.AsNoTracking().Where(category => category.Active).OrderBy(category => category.Name).Select(category => new CategoryResponse(category.Id, category.Name, category.Type, category.Active)).ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Category name is required.");
        var category = new Category { Name = request.Name.Trim(), Type = request.Type, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        dbContext.Categories.Add(category); await dbContext.SaveChangesAsync(cancellationToken);
        return Created($"/api/categories/{category.Id}", new CategoryResponse(category.Id, category.Name, category.Type, category.Active));
    }
}
