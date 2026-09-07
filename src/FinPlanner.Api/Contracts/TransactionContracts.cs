using FinPlanner.Api.Domain;

namespace FinPlanner.Api.Contracts;

public sealed record TransactionResponse(
    Guid Id,
    Guid FamilyAccountId,
    string AccountName,
    string Description,
    decimal Amount,
    FamilyTransactionType Type,
    DateOnly TransactionDate,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CreateTransactionRequest(
    Guid FamilyAccountId,
    string Description,
    decimal Amount,
    FamilyTransactionType Type,
    DateOnly TransactionDate);

public sealed record UpdateTransactionRequest(
    Guid FamilyAccountId,
    string Description,
    decimal Amount,
    FamilyTransactionType Type,
    DateOnly TransactionDate);

public sealed record AccountResponse(Guid Id, string Name, FamilyAccountType Type, string Currency, bool IsActive);

public sealed record CreateAccountRequest(string Name, FamilyAccountType Type, string Currency = "EUR");