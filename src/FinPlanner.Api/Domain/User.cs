namespace FinPlanner.Api.Domain;

public enum UserRole { Owner, Member }

public sealed class User
{
    public long Id { get; set; }
    public long FamilyId { get; set; }
    public required string Name { get; set; }
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
