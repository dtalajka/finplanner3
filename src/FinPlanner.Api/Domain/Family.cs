namespace FinPlanner.Api.Domain;

public sealed class Family
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
