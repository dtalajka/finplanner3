namespace FinPlanner.Api.Domain;

public sealed class ExpenseAllocationShare
{
    public long Id { get; set; }
    public long FamilyId { get; set; }
    public long AllocationRuleId { get; set; }
    public long UserId { get; set; }
    public decimal? Percentage { get; set; }
    public decimal? FixedAmount { get; set; }
}
