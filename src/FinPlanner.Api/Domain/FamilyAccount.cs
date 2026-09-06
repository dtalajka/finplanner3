namespace FinPlanner.Api.Domain;

public enum FamilyAccountType
{
    Checking,
    Savings,
    Cash,
    CreditCard,
    Investment,
    Loan,
    Other
}

public sealed class FamilyAccount
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public FamilyAccountType Type { get; set; }

    public string Currency { get; set; } = "EUR";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
}