namespace FinPlanner.Api.Domain;

public sealed class ActualTransaction
{
    public long Id { get; set; }
    public DateOnly ActualDate { get; set; }
    public decimal Amount { get; set; }
    public long? FromAccountId { get; set; }
    public long? ToAccountId { get; set; }
    public long? CategoryId { get; set; }
    public long? PlannedTransactionId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}