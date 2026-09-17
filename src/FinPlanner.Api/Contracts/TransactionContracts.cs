using FinPlanner.Api.Domain;

namespace FinPlanner.Api.Contracts;

public sealed record UserResponse(long Id, string Name, UserRole Role, long FamilyId, string Email);
public sealed record RegisterRequest(string FamilyName, string Name, string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record CreateFamilyMemberRequest(string Name, string Email, string Password, UserRole Role);

public sealed record PlanResponse(long Id, string Name, string? Description, bool IsDefault, bool IsActive, decimal? MinimumReserve);
public sealed record CreatePlanRequest(string Name, string? Description);
public sealed record SetPlanReserveRequest(decimal? MinimumReserve);

public sealed record FamilyResponse(long Id, string Name, decimal DefaultMinimumReserve);
public sealed record UpdateFamilyReserveRequest(decimal DefaultMinimumReserve);

public sealed record AccountResponse(long Id, string Name, string Currency, decimal OpeningBalance, long? OwnerUserId, bool Active);
public sealed record CreateAccountRequest(string Name, string Currency = "EUR", decimal OpeningBalance = 0, long? OwnerUserId = null);
public sealed record UpdateAccountRequest(string Name, string Currency, decimal OpeningBalance, bool Active, long? OwnerUserId = null);

public sealed record TransactionResponse(long Id, DateOnly Date, decimal Amount, long? FromAccountId, long? ToAccountId, long? CategoryId, long? PlannedTransactionId, string? Description, string TransactionType, long? GoalId);
public sealed record CreateTransactionRequest(DateOnly Date, decimal Amount, long? FromAccountId, long? ToAccountId, long? CategoryId, long? PlannedTransactionId, string? Description, long? GoalId = null);
public sealed record UpdateTransactionRequest(DateOnly Date, decimal Amount, long? FromAccountId, long? ToAccountId, long? CategoryId, long? PlannedTransactionId, string? Description, long? GoalId = null);

public sealed record CategoryResponse(long Id, string Name, CategoryType Type, bool Active);
public sealed record CreateCategoryRequest(string Name, CategoryType Type);

public sealed record PlannedTransactionResponse(long Id, long PlanId, DateOnly Date, decimal Amount, long? FromAccountId, long? ToAccountId, long? CategoryId, long? RecurringRuleId, string? Description, bool Cancelled, string TransactionType, long? GoalId);
public sealed record RecurringRuleResponse(long Id, long PlanId, string Name, decimal Amount, long? FromAccountId, long? ToAccountId, long? CategoryId, RecurrenceFrequency Frequency, short? DayOfMonth, DateOnly StartDate, DateOnly? EndDate, bool Active, string? Description, string TransactionType);
public sealed record CreatePlannedTransactionRequest(DateOnly Date, decimal Amount, long? FromAccountId, long? ToAccountId, long? CategoryId, long? RecurringRuleId, string? Description, long? PlanId = null, long? GoalId = null);
public sealed record UpdatePlannedTransactionRequest(DateOnly Date, decimal Amount, long? FromAccountId, long? ToAccountId, long? CategoryId, string? Description, long? GoalId = null);
public sealed record CreateRecurringRuleRequest(string Name, decimal Amount, long? FromAccountId, long? ToAccountId, long? CategoryId, RecurrenceFrequency Frequency, short? DayOfMonth, DateOnly StartDate, DateOnly? EndDate, string? Description, long? PlanId = null);
public sealed record UpdateRecurringRuleRequest(string Name, decimal Amount, long? FromAccountId, long? ToAccountId, long? CategoryId, RecurrenceFrequency Frequency, short? DayOfMonth, DateOnly StartDate, DateOnly? EndDate, string? Description);

public sealed record GoalResponse(long Id, long PlanId, string Name, decimal TargetAmount, DateOnly TargetDate, GoalPriority Priority, long? RecurringRuleId, string? Description, bool Active, decimal CurrentFunding, decimal RemainingAmount, decimal RequiredMonthlyContribution, string Status);
public sealed record CreateGoalRequest(string Name, decimal TargetAmount, DateOnly TargetDate, GoalPriority Priority, long? RecurringRuleId, string? Description, long? PlanId = null);
public sealed record UpdateGoalRequest(string Name, decimal TargetAmount, DateOnly TargetDate, GoalPriority Priority, long? RecurringRuleId, string? Description);

public sealed record ForecastAccountResponse(long AccountId, string Name, decimal CurrentBalance);
public sealed record ForecastEventResponse(DateOnly Date, decimal Amount, long? FromAccountId, long? ToAccountId, long? CategoryId, long? GoalId, string TransactionType, string Source, string Status, long? PlannedTransactionId, long? RecurringRuleId, string? Description);
public sealed record ForecastTrajectoryPointResponse(DateOnly Date, IReadOnlyDictionary<long, decimal> AccountBalances, decimal TotalBalance);
public sealed record ForecastResponse(long PlanId, DateOnly AsOf, DateOnly HorizonEnd, IReadOnlyList<ForecastAccountResponse> Accounts, IReadOnlyList<ForecastEventResponse> Events, IReadOnlyList<ForecastTrajectoryPointResponse> Trajectory, decimal MinimumFutureBalance, DateOnly MinimumFutureBalanceDate, bool NegativeBalanceRisk);

public sealed record GoalFeasibilityResponse(long GoalId, string Name, GoalPriority Priority, string Status);
public sealed record AvailableToSpendResponse(long PlanId, DateOnly AsOf, DateOnly HorizonEnd, decimal MinimumReserve, decimal ForecastMinBalance, DateOnly ForecastMinBalanceDate, decimal AvailableToSpend, decimal ReserveShortfall, string PlanStatus, IReadOnlyList<GoalFeasibilityResponse> Goals);

public sealed record CategoryComplianceResponse(long CategoryId, string CategoryName, CategoryType CategoryType, decimal PlannedAmount, decimal ActualAmount, decimal Variance, decimal? VariancePercent, string Favorability, int PlannedRealCount, int PlannedRecurringCount, int ActualMatchedCount, int ActualUnmatchedCount, double? AverageTimingVarianceDays);
public sealed record GoalComplianceResponse(long GoalId, string GoalName, GoalPriority Priority, decimal PlannedContribution, decimal ActualContribution, decimal Variance, decimal? VariancePercent, string Favorability);
public sealed record ComplianceTotalsResponse(decimal PlannedIncome, decimal ActualIncome, decimal IncomeVariance, decimal PlannedExpense, decimal ActualExpense, decimal ExpenseVariance, decimal NetVariance, decimal? CategoriesOnPlanRatio);
public sealed record PeriodComplianceResponse(DateOnly PeriodStart, DateOnly PeriodEnd, string Status, IReadOnlyList<CategoryComplianceResponse> Categories, IReadOnlyList<GoalComplianceResponse> Goals, ComplianceTotalsResponse Totals);
public sealed record PlanComplianceResponse(long PlanId, DateOnly AsOf, DateOnly From, DateOnly To, IReadOnlyList<PeriodComplianceResponse> Periods, ComplianceTotalsResponse Totals);

public sealed record AllocationShareRequest(long UserId, decimal? Percentage, decimal? FixedAmount);
public sealed record AllocationShareResponse(long UserId, decimal? Percentage, decimal? FixedAmount);
public sealed record CreateAllocationRuleRequest(long? CategoryId, ExpenseAllocationMethod Method, IReadOnlyList<AllocationShareRequest> Shares);
public sealed record UpdateAllocationRuleRequest(long? CategoryId, ExpenseAllocationMethod Method, IReadOnlyList<AllocationShareRequest> Shares);
public sealed record AllocationRuleResponse(long Id, long? CategoryId, ExpenseAllocationMethod Method, bool Active, IReadOnlyList<AllocationShareResponse> Shares);

public sealed record UserSettlementResponse(long UserId, string Name, decimal TotalPaid, decimal TotalExpectedContribution, decimal Settlement);
public sealed record CategorySettlementResponse(long? CategoryId, string CategoryName, string Method, decimal TotalAmount, IReadOnlyDictionary<long, decimal> PerUserExpected);
public sealed record SettlementResponse(long FamilyId, DateOnly From, DateOnly To, IReadOnlyList<UserSettlementResponse> Users, decimal PoolContribution, decimal UnattributedAmount, IReadOnlyList<CategorySettlementResponse> Categories);
