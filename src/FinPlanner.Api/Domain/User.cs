namespace FinPlanner.Api.Domain;

public enum UserRole { Owner, Member }

public sealed class User
{
    public long Id { get; set; }
    public long FamilyId { get; set; }
    public required string Name { get; set; }
    // Case-insensitive login identifier: always stored normalized to lowercase (see AuthController.NormalizeEmail).
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
