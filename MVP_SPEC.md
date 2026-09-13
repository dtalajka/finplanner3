# MVP Specification

## 1. Purpose

Build the smallest complete version that proves the central product hypothesis:

> A household financial planning engine can calculate how much discretionary money is safely available while considering future obligations and goals.

The MVP must be usable end-to-end rather than being a collection of disconnected CRUD screens.

---

# 2. MVP user story

As a family member, I want to:

1. define our accounts,
2. record actual income and expenses,
3. define our expected future transactions,
4. define savings goals,
5. select a financial plan,
6. see a future cash-flow forecast,
7. see how much I can safely spend,
8. change an assumption and immediately see the impact.

---

# 3. Core domain

## 3.1 Tenant / Family

A tenant represents one independent family.

Fields conceptually:

```text
id
name
created_at
updated_at
```

All tenant-owned data must reference the tenant.

---

## 3.2 User

A user belongs to a family.

Minimum MVP roles:

```text
OWNER
MEMBER
```

The authorization model may be implemented simply for MVP, but tenant isolation must already be correct.

---

## 3.3 Account

Represents a real family financial account.

Examples:

```text
Salary A
Salary B
Mortgage
Savings
```

Minimum fields:

```text
id
tenant_id
name
currency
opening_balance
active
```

---

## 3.4 Category

Represents external income or expense classification.

Types:

```text
INCOME
EXPENSE
```

Transfers do not require a category.

---

# 4. Actual transactions

Actual transactions represent what really happened.

### Income

```text
from_account = NULL
to_account = account
category = INCOME
```

### Expense

```text
from_account = account
to_account = NULL
category = EXPENSE
```

### Internal transfer

```text
from_account = source
to_account = destination
category = NULL
```

One internal transfer is one transaction.

Required behavior:

- transaction date,
- amount,
- source/destination account,
- category when applicable,
- description,
- optional link to planned transaction.

Money must use PostgreSQL `NUMERIC`, not floating point.

---

# 5. Plans

A family can have multiple plans.

Minimum fields:

```text
id
tenant_id
name
description
active
```

Only one plan needs to be selected as the current/default plan for MVP, but the data model must allow multiple plans.

Example:

```text
Normal
Expensive vacation
New car
Lower income
```

Plans represent future assumptions.

Actual transactions are NOT copied into plans.

---

# 6. Planned transactions

A planned transaction belongs to a plan.

It represents an expected future money movement.

Minimum fields:

```text
id
plan_id
planned_date
amount
from_account_id
to_account_id
category_id
recurring_rule_id
description
cancelled
```

Rules:

- at least one account endpoint must exist,
- source and destination cannot be the same account,
- internal transfers have no category,
- external income/expense may have a category.

---

# 7. Recurring rules

Recurring rules generate planned transactions.

MVP frequencies:

```text
MONTHLY
YEARLY
```

The architecture may support more later.

Examples:

```text
Salary A
€2,000 monthly

Salary B
€3,000 monthly

Mortgage
€1,200 monthly

Life insurance contribution
€100 monthly
```

Recurring rules should be templates, not actual transactions.

---

# 8. Goals / sinking funds

A goal represents a future financial target.

Minimum fields:

```text
id
tenant_id
plan_id
name
target_amount
target_date
priority
```

Priority:

```text
HARD
SOFT
```

Optional MVP fields:

```text
current_funding
monthly_contribution
```

Examples:

```text
Life insurance
Target: €1,200
Due: 12 months

Vacation
Target: €2,400
Due: 8 months
```

The system must calculate the required contribution needed to meet a target.

For a simple goal:

```text
required_monthly_contribution =
    remaining_amount / remaining_periods
```

The calculation must handle rounding and the final contribution correctly.

---

# 9. Shared expense allocation

MVP must support generic allocation.

Do not create special mortgage logic.

Allocation methods:

```text
FIXED_PERCENTAGE
EQUAL
INCOME_RATIO
```

A later version may add:

```text
FIXED_AMOUNT
CUSTOM
```

Example:

```text
Person A income = €2,000
Person B income = €3,000

Income ratio:
A = 40%
B = 60%
```

For:

```text
Mortgage = €1,200
```

expected economic shares:

```text
A = €480
B = €720
```

The physical payment account may be different.

---

# 10. Contribution and settlement

The MVP should at least calculate contribution differences.

Example:

Expected:

```text
A should bear €480
B should bear €720
```

Actual:

```text
A paid €700
B paid €500
```

Difference:

```text
A overpaid €220
B underpaid €220
```

The system should expose this as a settlement result.

A physical settlement transaction does not need to be automatically created in the first MVP.

---

# 11. Forecast engine

The forecast engine is the core business logic.

Input:

```text
current balances
actual transactions
future planned transactions
goals
selected plan
forecast horizon
minimum reserve
```

Output should include at least:

```text
date
account balances
total balance
planned inflow
planned outflow
forecast balance
```

Also calculate:

```text
minimum_future_balance
minimum_future_balance_date
negative_balance_risk
```

---

# 12. Actual vs planned handling

The forecast must avoid double counting.

For a planned transaction:

```text
planned €800
actual €823.47
```

After the actual occurs:

- actual affects historical/current balance,
- planned transaction must not also affect the forecast.

The actual transaction becomes the source of truth for that occurrence.

---

# 13. Available-to-spend calculation

The MVP should implement a deterministic version.

Conceptual algorithm:

1. Start from actual current balances.
2. Project all future committed inflows/outflows.
3. Include required goal funding.
4. Respect minimum reserve.
5. Determine the maximum additional discretionary spending that can occur without violating the plan.

The calculation must identify the point in the forecast where the family has the least financial headroom.

Do not simply calculate:

```text
current balance - this month's expenses
```

The calculation must look into the future.

---

# 14. Suggested MVP algorithm

For a given plan:

```text
current_cash
    +
future_expected_income
    -
future_required_expenses
    -
required_goal_funding
    -
minimum_reserve
    =
future_discretionary_headroom
```

Then determine the maximum immediate discretionary expense that preserves:

```text
forecast_balance >= minimum_reserve
```

for the selected forecast horizon.

Important:

This is an initial deterministic model. The implementation should isolate the calculation so that the policy can evolve without rewriting the transaction model.

---

# 15. Forecast horizon

MVP default:

```text
12 months
```

Allow configuration later.

The UI should clearly show that "available to spend" depends on the selected horizon and assumptions.

---

# 16. What-if scenario

The first what-if operation should be simple:

```text
Change vacation target:
€2,400 → €3,500
```

The application calculates the scenario without modifying the base plan.

Display:

```text
Base plan
    Available to spend: €X
    Minimum future balance: €Y

Scenario
    Available to spend: €A
    Minimum future balance: €B

Difference
    Available to spend: €A-X
```

A scenario can initially be an in-memory copy of a plan.

Persistent scenario storage is not required for MVP if that simplifies implementation.

---

# 17. MVP dashboard

The first UI should prioritize decisions, not CRUD.

Example:

```text
--------------------------------------------------
AVAILABLE TO SPEND
€620
--------------------------------------------------

Plan
Normal

Plan status
ON TRACK

Current total balance
€4,850

Lowest projected balance
€1,240
15.04.2027

Next major obligation
Life insurance
€1,200
15.03.2027

Vacation
€1,400 / €2,400

Shared expenses
A: 40%
B: 60%

--------------------------------------------------
WHAT IF?
--------------------------------------------------

Vacation +€1,100

Available to spend
€410

Impact
-€210
--------------------------------------------------
```

Exact visual design is not important for the first implementation.

Correctness and clarity are more important.

---

# 18. API expectations

The exact API framework is implementation-dependent.

Conceptually the MVP needs endpoints/services for:

```text
families
users
accounts
categories
actual transactions
plans
planned transactions
recurring rules
goals
forecast
available-to-spend
scenario simulation
shared expense allocation
settlement
```

Forecast and available-to-spend calculations should preferably be exposed through application services rather than embedding all business logic in controllers.

---

# 19. Testing requirements

Financial calculations require automated tests.

Minimum test cases:

### Transactions

- income increases destination account,
- expense decreases source account,
- transfer decreases source and increases destination,
- transfer has no category.

### Planned vs actual

- planned only,
- actual only,
- planned + matched actual,
- cancelled planned,
- overdue planned.

### Recurring

- monthly generation,
- yearly generation,
- start date,
- end date.

### Goals

- exact target,
- partially funded target,
- target already reached,
- target date in current period,
- rounding of monthly contributions.

### Allocation

- 40/60 allocation,
- equal allocation,
- income-ratio allocation,
- 100/0 personal expense,
- settlement calculation.

### Forecast

- future income,
- future expense,
- future transfer,
- goal funding,
- minimum reserve,
- negative balance detection,
- matched actual not double-counted.

### Available to spend

At least:

1. enough cash,
2. tight future obligation,
3. future annual obligation,
4. vacation goal,
5. income reduction,
6. increased vacation cost.

---

# 20. Example acceptance test

Given:

```text
Income A       €2,000
Income B       €3,000

Mortgage       €1,200
Electricity      €150
Internet          €30
Groceries        €600
```

Allocation:

```text
A = 40%
B = 60%
```

Goals:

```text
Life insurance  €1,200 in 12 months
Vacation        €2,400 in 8 months
```

The system must:

1. calculate the current account balances,
2. calculate future planned cash flow,
3. calculate required goal funding,
4. detect the lowest projected balance,
5. calculate available-to-spend,
6. show the selected plan as on-track or at-risk.

Then change:

```text
Vacation = €3,500
```

The system must show a lower available-to-spend value and/or a changed future forecast.

---

# 21. Implementation constraints

Do not add unrelated features during MVP.

Do not introduce:

- investment management,
- bank aggregation,
- accounting-grade double-entry,
- tax calculation,
- cryptocurrency,
- complex permissions,
- complex reporting.

Do not optimize prematurely.

Do not hide financial rules in UI code.

Business calculations should be deterministic, isolated and unit-testable.

---

# 22. Definition of Done

MVP is done when a fresh family can:

1. create users,
2. create accounts,
3. enter actual transactions,
4. create a plan,
5. define recurring income/expenses,
6. create at least two goals,
7. view a 12-month forecast,
8. see available-to-spend,
9. see shared expense allocation,
10. run the vacation-cost what-if scenario,
11. see plan-vs-actual differences,
12. run automated tests proving the financial calculations.

The application should be demonstrable using only the MVP scenario described in this document.
