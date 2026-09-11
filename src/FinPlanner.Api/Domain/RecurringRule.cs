namespace FinPlanner.Api.Domain;

public enum RecurrenceFrequency { Daily, Weekly, Monthly, Yearly }

public sealed class RecurringRule
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public long? FromAccountId { get; set; }
    public long? ToAccountId { get; set; }
    public long? CategoryId { get; set; }
    public decimal Amount { get; set; }
    public RecurrenceFrequency Frequency { get; set; }
    public short? DayOfMonth { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool Active { get; set; } = true;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}