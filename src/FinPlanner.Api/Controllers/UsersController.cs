using FinPlanner.Api.Contracts;
using FinPlanner.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinPlanner.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(FinPlannerDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> List(CancellationToken cancellationToken) => Ok(
        await dbContext.Users.AsNoTracking().OrderBy(user => user.FamilyId).ThenBy(user => user.Name)
            .Select(user => new UserResponse(user.Id, user.Name, user.Role, user.FamilyId)).ToListAsync(cancellationToken));
}
