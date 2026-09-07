namespace FinPlanner.Api.Domain;

public enum FamilyTransactionType
{
    Income,
    Expense
}

public sealed class FamilyTransaction
{
    public Guid Id { get; set; }

    public Guid FamilyAccountId { get; set; }

    public required string Description { get; set; }

    public decimal Amount { get; set; }

    public FamilyTransactionType Type { get; set; }

    public DateOnly TransactionDate { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public FamilyAccount FamilyAccount { get; set; } = null!;
}