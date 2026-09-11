namespace FinPlanner.Api.Domain;

public enum CategoryType { Income, Expense }

public sealed class Category
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public CategoryType Type { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}