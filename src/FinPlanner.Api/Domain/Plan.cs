namespace FinPlanner.Api.Domain;

public sealed class Plan
{
    public long Id { get; set; }
    public long FamilyId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal? MinimumReserve { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
