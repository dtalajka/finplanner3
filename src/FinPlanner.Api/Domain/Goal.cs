namespace FinPlanner.Api.Domain;

public enum GoalPriority { Hard, Soft }

public sealed class Goal
{
    public long Id { get; set; }
    public long FamilyId { get; set; }
    public long PlanId { get; set; }
    public required string Name { get; set; }
    public decimal TargetAmount { get; set; }
    public DateOnly TargetDate { get; set; }
    public GoalPriority Priority { get; set; }
    public long? RecurringRuleId { get; set; }
    public string? Description { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
