namespace FinPlanner.Api.Domain;

public sealed class Account
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal OpeningBalance { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}