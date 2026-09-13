namespace FinPlanner.Api.Security;

public sealed class CurrentUserContext
{
    public long? UserId { get; set; }
    public long? FamilyId { get; set; }
}
