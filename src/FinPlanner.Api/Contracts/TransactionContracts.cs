using FinPlanner.Api.Domain;

namespace FinPlanner.Api.Contracts;

public sealed record AccountResponse(long Id, string Name, string Currency, decimal OpeningBalance, bool Active);
public sealed record CreateAccountRequest(string Name, string Currency = "EUR", decimal OpeningBalance = 0);
public sealed record UpdateAccountRequest(string Name, string Currency, decimal OpeningBalance, bool Active);

public sealed record TransactionResponse(long Id, DateOnly Date, decimal Amount, long? FromAccountId, long? ToAccountId, long? CategoryId, long? PlannedTransactionId, string? Description, string TransactionType);
public sealed record CreateTransactionRequest(DateOnly Date, decimal Amount, long? FromAccountId, long? ToAccountId, long? CategoryId, long? PlannedTransactionId, string? Description);
public sealed record UpdateTransactionRequest(DateOnly Date, decimal Amount, long? FromAccountId, long? ToAccountId, long? CategoryId, long? PlannedTransactionId, string? Description);

public sealed record CategoryResponse(long Id, string Name, CategoryType Type, bool Active);
public sealed record CreateCategoryRequest(string Name, CategoryType Type);

public sealed record PlannedTransactionResponse(long Id, DateOnly Date, decimal Amount, long? FromAccountId, long? ToAccountId, long? CategoryId, long? RecurringRuleId, string? Description, bool Cancelled);
public sealed record RecurringRuleResponse(long Id, string Name, decimal Amount, RecurrenceFrequency Frequency, DateOnly StartDate, DateOnly? EndDate, bool Active, string? Description);
public sealed record CreatePlannedTransactionRequest(DateOnly Date, decimal Amount, long? FromAccountId, long? ToAccountId, long? CategoryId, long? RecurringRuleId, string? Description);
public sealed record CreateRecurringRuleRequest(string Name, decimal Amount, long? FromAccountId, long? ToAccountId, long? CategoryId, RecurrenceFrequency Frequency, short? DayOfMonth, DateOnly StartDate, DateOnly? EndDate, string? Description);
