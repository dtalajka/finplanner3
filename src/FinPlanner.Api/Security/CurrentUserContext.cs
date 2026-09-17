using FinPlanner.Api.Domain;

namespace FinPlanner.Api.Security;

public sealed class CurrentUserContext
{
    public long? UserId { get; set; }
    public long? FamilyId { get; set; }
    public UserRole? Role { get; set; }
}
