# Family Financial Planning

A multi-tenant household financial planning and cash-flow simulation application.

The project is designed around one central question:

> **How much can we safely spend now while keeping our chosen financial plan viable in the future?**

This is intentionally more than a traditional budgeting application. Transaction tracking, budgeting and reporting support the main purpose: **planning, forecasting and decision making**.

## Core concepts

```text
FAMILY / TENANT
    ├── USERS
    ├── ACCOUNTS
    ├── ACTUAL TRANSACTIONS
    ├── CATEGORIES
    ├── SHARED EXPENSE ALLOCATIONS
    └── PLANS
          ├── PLANNED TRANSACTIONS
          ├── RECURRING RULES
          └── GOALS / SINKING FUNDS
                    │
                    ▼
               FORECAST
                    │
                    ▼
           AVAILABLE TO SPEND
                    │
                    ▼
               WHAT-IF
```

## Product goals

A family should be able to answer:

1. How much money do we really have?
2. How much is already committed?
3. How should shared expenses be divided?
4. Are we following our selected plan?
5. Will our future cash position remain healthy?
6. How much can we safely spend now?
7. What happens if income or expenses change?
8. Which financial plan is sustainable?

## Important distinction

The system separates:

- **Actual** — what really happened.
- **Plan** — what we expect or want to happen.
- **Goal / sinking fund** — a future target funded over time.
- **Expense** — economic cost.
- **Payment** — movement of money between accounts.
- **Allocation** — who economically bears an expense.
- **Contribution** — what a family member should provide.
- **Settlement** — transfer needed to restore fair contribution.
- **Forecast** — projected future financial state.
- **Available to spend** — discretionary amount that can be spent without breaking the selected plan.

## Multi-tenancy

Tenant = family.

Each family has:

- multiple users,
- multiple accounts,
- actual financial history,
- multiple plans/scenarios.

Data must be isolated between tenants.

## Plans

A family can maintain multiple alternative scenarios:

- Normal
- Expensive vacation
- New car
- Lower income
- Aggressive saving
- Conservative

Actual transactions belong to the family reality and are shared by plans. Plans contain alternative future assumptions.

## Shared expenses

Shared-cost allocation is generic. Do not create mortgage-specific business logic.

Possible allocation methods:

- FIXED_PERCENTAGE
- FIXED_AMOUNT
- INCOME_RATIO
- EQUAL
- CUSTOM

Example:

```text
Mortgage €1,200
A = 40%
B = 60%

A share = €480
B share = €720
```

The account that physically pays does not necessarily correspond to the person who economically bears the expense.

## Forecasting

The forecast combines:

- current account balances,
- actual transactions,
- future planned transactions,
- recurring rules,
- goals,
- income assumptions,
- shared-cost effects,
- minimum reserve,
- selected plan.

For completed periods, actuals represent reality.

For future periods, planned transactions represent expectations.

A matched planned transaction must never be counted twice.

## Available to spend

The central derived value is:

```text
available_to_spend
```

It should represent the maximum additional discretionary spending that can occur while the selected plan remains viable over the configured forecast horizon.

The calculation must explicitly define assumptions such as:

- forecast horizon,
- minimum reserve,
- hard vs soft commitments,
- treatment of uncertain expenses,
- income assumptions,
- safety margin.

Do not implement a simplistic:

```text
income - expenses = available
```

calculation.

## MVP

The first end-to-end scenario uses:

### Family

```text
Person A
Person B
```

### Accounts

```text
Salary A
Salary B
Mortgage
Savings
```

### Monthly income

```text
A: €2,000
B: €3,000
Total: €5,000
```

### Shared expenses

```text
Mortgage:   €1,200
Electricity:  €150
Internet:      €30
Groceries:    €600
```

### Allocation

```text
A = 40%
B = 60%
```

### Goals

```text
Life insurance:
    €1,200
    due in 12 months

Vacation:
    €2,400
    due in 8 months
```

The system should calculate:

```text
current balances
        +
expected income
        -
expected expenses
        -
goal funding
        -
future obligations
        =
forecast
        ↓
available_to_spend
```

Then support a scenario change such as:

```text
Vacation cost changes from €2,400 to €3,500
```

and show the impact on:

- future balances,
- plan status,
- goal funding,
- available-to-spend.

## Non-goals for MVP

Do not build these initially:

- investment portfolio management,
- stock trading,
- cryptocurrency,
- complex bank aggregation,
- accounting-grade double-entry bookkeeping,
- tax calculation,
- advanced financial products.

These can be considered only after the core planning engine is proven.

## Technology

PostgreSQL is the preferred database.

The exact application stack is intentionally not prescribed by this document. If an existing repository exists, inspect and preserve its conventions before introducing a new stack.

## Development principles

Financial correctness is more important than convenience.

Use decimal/numeric types for money.

Keep domain logic deterministic and testable.

Prefer explicit domain concepts.

Use database constraints where they improve correctness.

Every tenant-owned query must be tenant-safe.

Important calculations require automated tests.

## Development order

Recommended implementation sequence:

1. Repository and architecture inspection
2. Tenant/family and users
3. Accounts and categories
4. Actual transactions
5. Plans
6. Planned transactions and recurring rules
7. Goals / sinking funds
8. Forecast engine
9. Available-to-spend
10. Shared-cost allocation
11. What-if scenarios
12. UI refinement and reporting

## Success criterion

The application is not successful merely because it can record transactions.

It is successful when a family can look at it and confidently answer:

> **"How much can I spend today without breaking the plan?"**
