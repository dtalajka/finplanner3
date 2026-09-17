namespace FinPlanner.Api.Domain;

public enum ExpenseAllocationMethod { FixedPercentage, Equal, IncomeRatio, FixedAmount }

public sealed class ExpenseAllocationRule
{
    public long Id { get; set; }
    public long FamilyId { get; set; }
    public long? CategoryId { get; set; }
    public ExpenseAllocationMethod Method { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
