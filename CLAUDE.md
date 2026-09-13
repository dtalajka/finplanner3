# CLAUDE.md — Family Financial Planning Application

## 1. Project purpose

Build a multi-tenant family financial planning application.

The application is **not primarily a traditional budgeting app**. Its core purpose is to answer:

> **"How much can we safely spend now/this month while still keeping our chosen financial plan viable in the future?"**

The system combines:

- actual financial transactions,
- future planned transactions,
- recurring obligations,
- savings goals / sinking funds,
- multiple family members,
- multiple bank accounts,
- fair allocation of shared household expenses,
- financial plans/scenarios,
- actual-vs-plan comparison,
- future cash-flow forecasting,
- what-if scenario simulation,
- and an `available_to_spend` calculation.

The project should be approached as a **financial planning / cash-flow forecasting system**, not merely as another expense tracker.

---

## 2. Important product philosophy

Before implementing features, keep this distinction clear:

### Actual reality

What really happened:

- salary arrived,
- electricity was paid,
- mortgage was paid,
- money was transferred between family accounts,
- vacation was paid,
- unexpected expense occurred.

Actual transactions belong to the **family's financial reality**, not to one particular plan.

### Plan

A plan describes what the family expects/wants to happen in the future.

A family can have several plans at the same time:

- Normal plan
- Expensive vacation
- New car
- Lower income
- Aggressive saving
- Conservative plan

Plans are alternative future scenarios.

### Goal / sinking fund

A future obligation or target that is funded gradually.

Examples:

- annual life insurance: €960/year → save €80/month
- vacation: €2,400 in July → save monthly
- car maintenance
- annual property tax
- Christmas budget

The application must understand that the monthly contribution is not simply "free spending"; it is money reserved for a future purpose.

### Available to spend

This is the most important derived value.

It should answer:

> Given current balances, actual transactions, future obligations, goals, income assumptions, minimum reserves and the selected plan, how much additional money can be spent without causing the future plan to fail?

This should be a first-class concept in the application.

---

# 3. Multi-tenancy

The application must support multiple independent families.

Conceptually:

```text
TENANT / FAMILY
    ├── USERS
    ├── ACCOUNTS
    ├── ACTUAL TRANSACTIONS
    ├── CATEGORIES
    ├── SHARED EXPENSE ALLOCATIONS
    ├── RECURRING RULES
    └── PLANS
          ├── planned transactions
          ├── goals / sinking funds
          └── scenario assumptions
```

Rules:

- Every family is isolated from every other family.
- A user may belong to a family.
- A family may have multiple users.
- A family may have multiple financial plans.
- All tenant-owned data must be tenant-scoped.
- Never allow cross-tenant data access.

The exact authentication/authorization implementation can be selected later, but the domain model must support this from the beginning.

---

# 4. Family users and accounts

A family may have multiple members and multiple bank accounts.

Example:

```text
Family
 ├── User A
 ├── User B
 │
 ├── Salary account A
 ├── Salary account B
 ├── Mortgage account
 └── Savings account
```

The application must distinguish:

- who owns/uses an account,
- who physically pays a transaction,
- and who economically bears the expense.

These are NOT necessarily the same thing.

---

# 5. Shared expense allocation

Do NOT implement special logic specifically for mortgages.

Mortgage is just one example of a generic shared-cost problem.

Example:

Mortgage = €1,000

Salary contribution ratio:

- Person A = 40%
- Person B = 60%

The mortgage payment may physically leave the mortgage account, but economically:

```text
A contribution = €400
B contribution = €600
```

Another example:

Electricity = €100

It may be paid from A's account but allocated:

```text
A = 40%
B = 60%
```

Another expense:

Internet = €30

```text
A = 50%
B = 50%
```

Personal expense:

```text
A = 100%
B = 0%
```

The model should therefore represent generic:

```text
EXPENSE
    ↓
ALLOCATION
    ↓
PAYMENT
    ↓
CONTRIBUTION / SETTLEMENT
```

Possible allocation methods:

- `FIXED_PERCENTAGE`
- `FIXED_AMOUNT`
- `INCOME_RATIO`
- `EQUAL`
- `CUSTOM`

The system should eventually be able to calculate whether one family member has paid more or less than their fair share and whether a settlement transfer is appropriate.

Do not hard-code concepts such as "mortgage contribution".

---

# 6. Transaction model

The system uses one transaction representation for money movement.

Semantic types:

### External income

```text
from_account_id = NULL
to_account_id   = account
category_id     = income category
```

### External expense

```text
from_account_id = account
to_account_id   = NULL
category_id     = expense category
```

### Internal transfer

```text
from_account_id = source account
to_account_id   = destination account
category_id     = NULL
```

An internal transfer is ONE transaction, not two unrelated transactions.

External accounts do not need to be represented as account rows unless a future feature explicitly requires that.

---

# 7. Actual vs planned transactions

Keep actual and planned transactions separate.

### Planned transaction

Represents something expected according to a plan.

### Actual transaction

Represents what actually happened.

Relationship:

```text
planned_transaction 1 ─── 0..1 actual_transaction
```

A planned transaction can:

- have no actual transaction yet,
- be matched to exactly one actual transaction,
- be cancelled.

An actual transaction can:

- match a planned transaction,
- or be completely unplanned.

Examples:

```text
Planned mortgage: €800
Actual mortgage:  €823.47

variance = +€23.47
```

Unplanned actual:

```text
Car repair €420
```

This should appear as `UNPLANNED`.

---

# 8. Recurring rules

A recurring rule is a template/prescription for generating planned transactions.

Examples:

```text
Salary
Monthly
€3,500
```

```text
Rent
Monthly
€700
```

```text
Life insurance sinking fund
Monthly
€80
```

```text
Vacation savings
Monthly
€200
```

Recurring rules should not themselves be treated as actual transactions.

They generate future planned transactions.

---

# 9. Existing PostgreSQL domain model

The original prototype model uses schema `finance`.

## account

```sql
CREATE TABLE finance.account (
    id                  BIGSERIAL PRIMARY KEY,
    name                TEXT NOT NULL,
    currency            CHAR(3) NOT NULL DEFAULT 'EUR',
    opening_balance     NUMERIC(14,2) NOT NULL DEFAULT 0,
    active              BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT account_currency_chk
        CHECK (currency ~ '^[A-Z]{3}$')
);
```

## category

```sql
CREATE TABLE finance.category (
    id                  BIGSERIAL PRIMARY KEY,
    name                TEXT NOT NULL,
    type                VARCHAR(10) NOT NULL,
    active              BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT category_type_chk
        CHECK (type IN ('INCOME', 'EXPENSE')),
    CONSTRAINT category_name_uq
        UNIQUE (name)
);
```

Transfers use `category_id IS NULL`.

## recurring_rule

```sql
CREATE TABLE finance.recurring_rule (
    id                  BIGSERIAL PRIMARY KEY,
    name                TEXT NOT NULL,
    from_account_id     BIGINT REFERENCES finance.account(id),
    to_account_id       BIGINT REFERENCES finance.account(id),
    category_id         BIGINT REFERENCES finance.category(id),
    amount              NUMERIC(14,2) NOT NULL,
    frequency           VARCHAR(10) NOT NULL,
    day_of_month        SMALLINT,
    start_date          DATE NOT NULL,
    end_date            DATE,
    active              BOOLEAN NOT NULL DEFAULT TRUE,
    description         TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT recurring_amount_chk CHECK (amount > 0),
    CONSTRAINT recurring_frequency_chk
        CHECK (frequency IN ('DAILY','WEEKLY','MONTHLY','YEARLY')),
    CONSTRAINT recurring_day_chk
        CHECK (day_of_month IS NULL OR day_of_month BETWEEN 1 AND 31),
    CONSTRAINT recurring_dates_chk
        CHECK (end_date IS NULL OR end_date >= start_date),
    CONSTRAINT recurring_account_chk
        CHECK (from_account_id IS NOT NULL OR to_account_id IS NOT NULL),
    CONSTRAINT recurring_same_account_chk
        CHECK (
            from_account_id IS NULL
            OR to_account_id IS NULL
            OR from_account_id <> to_account_id
        ),
    CONSTRAINT recurring_transfer_category_chk
        CHECK (
            (
                from_account_id IS NOT NULL
                AND to_account_id IS NOT NULL
                AND category_id IS NULL
            )
            OR
            (
                NOT (
                    from_account_id IS NOT NULL
                    AND to_account_id IS NOT NULL
                )
            )
        )
);
```

## planned_transaction

```sql
CREATE TABLE finance.planned_transaction (
    id                  BIGSERIAL PRIMARY KEY,
    planned_date        DATE NOT NULL,
    amount              NUMERIC(14,2) NOT NULL,
    from_account_id     BIGINT REFERENCES finance.account(id),
    to_account_id       BIGINT REFERENCES finance.account(id),
    category_id         BIGINT REFERENCES finance.category(id),
    recurring_rule_id   BIGINT REFERENCES finance.recurring_rule(id),
    description         TEXT,
    cancelled            BOOLEAN NOT NULL DEFAULT FALSE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT planned_amount_chk CHECK (amount > 0),
    CONSTRAINT planned_account_chk CHECK (
        from_account_id IS NOT NULL OR to_account_id IS NOT NULL
    ),
    CONSTRAINT planned_same_account_chk CHECK (
        from_account_id IS NULL
        OR to_account_id IS NULL
        OR from_account_id <> to_account_id
    ),
    CONSTRAINT planned_transfer_category_chk CHECK (
        (
            from_account_id IS NOT NULL
            AND to_account_id IS NOT NULL
            AND category_id IS NULL
        )
        OR
        (
            NOT (
                from_account_id IS NOT NULL
                AND to_account_id IS NOT NULL
            )
        )
    )
);
```

## actual_transaction

```sql
CREATE TABLE finance.actual_transaction (
    id                      BIGSERIAL PRIMARY KEY,
    actual_date             DATE NOT NULL,
    amount                  NUMERIC(14,2) NOT NULL,
    from_account_id         BIGINT REFERENCES finance.account(id),
    to_account_id           BIGINT REFERENCES finance.account(id),
    category_id             BIGINT REFERENCES finance.category(id),
    planned_transaction_id  BIGINT REFERENCES finance.planned_transaction(id),
    description             TEXT,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT actual_amount_chk CHECK (amount > 0),
    CONSTRAINT actual_account_chk CHECK (
        from_account_id IS NOT NULL OR to_account_id IS NOT NULL
    ),
    CONSTRAINT actual_same_account_chk CHECK (
        from_account_id IS NULL
        OR to_account_id IS NULL
        OR from_account_id <> to_account_id
    ),
    CONSTRAINT actual_transfer_category_chk CHECK (
        (
            from_account_id IS NOT NULL
            AND to_account_id IS NOT NULL
            AND category_id IS NULL
        )
        OR
        (
            NOT (
                from_account_id IS NOT NULL
                AND to_account_id IS NOT NULL
            )
        )
    )
);
```

Indexes:

```sql
CREATE INDEX idx_planned_transaction_date
    ON finance.planned_transaction (planned_date);

CREATE INDEX idx_planned_transaction_from_account
    ON finance.planned_transaction (from_account_id);

CREATE INDEX idx_planned_transaction_to_account
    ON finance.planned_transaction (to_account_id);

CREATE INDEX idx_planned_transaction_recurring_rule
    ON finance.planned_transaction (recurring_rule_id);

CREATE INDEX idx_actual_transaction_date
    ON finance.actual_transaction (actual_date);

CREATE INDEX idx_actual_transaction_from_account
    ON finance.actual_transaction (from_account_id);

CREATE INDEX idx_actual_transaction_to_account
    ON finance.actual_transaction (to_account_id);

CREATE INDEX idx_actual_transaction_planned
    ON finance.actual_transaction (planned_transaction_id);

CREATE UNIQUE INDEX uq_actual_transaction_planned
    ON finance.actual_transaction (planned_transaction_id)
    WHERE planned_transaction_id IS NOT NULL;
```

These tables are a starting point, not an immutable final schema. When introducing multi-tenancy, plans, users, goals, allocations, etc., evolve the model cleanly rather than blindly preserving an outdated prototype.

---

# 10. Existing transaction_timeline view

The original prototype has:

```sql
CREATE OR REPLACE VIEW finance.transaction_timeline AS
SELECT
    p.id AS planned_transaction_id,
    a.id AS actual_transaction_id,

    p.planned_date,
    a.actual_date,

    p.amount AS planned_amount,
    a.amount AS actual_amount,

    CASE
        WHEN p.amount IS NOT NULL
         AND a.amount IS NOT NULL
        THEN a.amount - p.amount
        ELSE NULL
    END AS amount_variance,

    p.from_account_id AS planned_from_account_id,
    p.to_account_id   AS planned_to_account_id,

    a.from_account_id AS actual_from_account_id,
    a.to_account_id   AS actual_to_account_id,

    p.category_id AS planned_category_id,
    a.category_id AS actual_category_id,

    p.recurring_rule_id,

    p.description AS planned_description,
    a.description AS actual_description,

    CASE
        WHEN p.id IS NOT NULL
         AND a.id IS NOT NULL
            THEN 'MATCHED'

        WHEN p.id IS NOT NULL
         AND a.id IS NULL
            THEN CASE
                WHEN p.cancelled
                    THEN 'CANCELLED'
                WHEN p.planned_date < CURRENT_DATE
                    THEN 'OVERDUE'
                ELSE 'PLANNED'
            END

        WHEN p.id IS NULL
         AND a.id IS NOT NULL
            THEN 'UNPLANNED'
    END AS status,

    CASE
        WHEN COALESCE(p.from_account_id, a.from_account_id) IS NOT NULL
         AND COALESCE(p.to_account_id, a.to_account_id) IS NOT NULL
            THEN 'TRANSFER'

        WHEN COALESCE(p.from_account_id, a.from_account_id) IS NULL
         AND COALESCE(p.to_account_id, a.to_account_id) IS NOT NULL
            THEN 'INCOME'

        WHEN COALESCE(p.from_account_id, a.from_account_id) IS NOT NULL
         AND COALESCE(p.to_account_id, a.to_account_id) IS NULL
            THEN 'EXPENSE'
    END AS transaction_type

FROM finance.planned_transaction p
FULL OUTER JOIN finance.actual_transaction a
    ON a.planned_transaction_id = p.id;
```

Important status values:

- `PLANNED`
- `MATCHED`
- `OVERDUE`
- `UNPLANNED`
- `CANCELLED`

The view is useful conceptually but should be reconsidered when the multi-tenant + multi-plan architecture is implemented.

---

# 11. Account cash-flow semantics

For forecasting, every transaction must produce account-level cash-flow effects.

```text
INCOME:
    to_account += amount

EXPENSE:
    from_account -= amount

TRANSFER:
    from_account -= amount
    to_account += amount
```

A future forecast must avoid double-counting a planned transaction whose actual transaction already occurred.

Conceptually:

```text
past/current reality:
    use ACTUAL

future:
    use PLAN

matched planned + actual:
    count the ACTUAL
    do not count the planned transaction again
```

A useful future abstraction may be something like:

```text
account_transaction_flow
```

followed by:

```text
account_balance_forecast
```

Do not prematurely optimize the SQL structure. First make the financial semantics correct and testable.

---

# 12. Forecasting

Forecasting is a central domain feature.

Given:

- current account balances,
- actual transactions,
- future planned transactions,
- recurring rules,
- goals,
- shared-cost allocations,
- income assumptions,
- selected plan,
- minimum reserve,

calculate future balances over a configurable horizon.

Example:

```text
Today
  |
  +-- salary
  +-- rent
  +-- electricity
  +-- groceries
  +-- life insurance sinking fund
  +-- vacation savings
  |
  +---------------------> future months
                          |
                          +-- annual insurance payment
                          +-- vacation payment
```

The forecast should be able to identify:

- lowest future balance,
- date of lowest balance,
- negative balance risk,
- reserve violations,
- goal shortfalls,
- future cash surplus.

---

# 13. Available-to-spend

This is a primary product capability.

A simplistic calculation:

```text
income - expenses = spendable
```

is NOT sufficient.

Example:

```text
Monthly income             3,500
Rent                         700
Electricity                  100
Internet                      30
Groceries                    500
Life insurance sinking fund   80
Vacation savings             200
Car sinking fund             150
Savings                      300
```

The remaining cash is not automatically all safe to spend because future obligations and goals must be considered.

The system should determine the maximum additional discretionary spending that can occur while the selected plan remains viable.

Potential outputs:

```text
available_to_spend
forecast_min_balance
minimum_required_reserve
goal_funding_status
plan_status
```

Do not hard-code one arbitrary definition of "safe".

The calculation needs explicit policy/assumptions such as:

- forecast horizon,
- minimum cash reserve,
- hard vs soft goals,
- treatment of uncertain expenses,
- income confidence,
- whether goals may be sacrificed,
- desired safety margin.

---

# 14. Hard vs soft commitments

This is an important concept to model.

Examples of hard commitments:

- mortgage
- rent
- electricity
- contractual insurance payment
- loan repayment
- taxes

Examples of soft/discretionary goals:

- vacation
- new car
- additional investment
- extra savings
- entertainment budget

The system should eventually allow plans to distinguish these.

This enables meaningful what-if behavior:

```text
"What if we reduce vacation savings?"

"What if income drops by 10%?"

"What if we buy a car?"

"What if electricity costs 20% more?"

"What if we postpone the vacation?"
```

---

# 15. Plans and scenarios

A family can have multiple plans.

Example:

```text
Plan: Normal
    Salary: €3,500
    Vacation: €2,400
    Car purchase: none

Plan: Expensive vacation
    Salary: €3,500
    Vacation: €4,000

Plan: New car
    Salary: €3,500
    Vacation: €2,400
    Car payment: €450/month

Plan: Lower income
    Salary: €3,150
    Vacation: €2,400
```

Actual transactions remain shared reality.

The plans represent different future assumptions.

A plan should be possible to clone and modify without duplicating actual transactions.

---

# 16. Important domain distinction

Keep these concepts separate:

```text
EXPENSE
    = economic cost

PAYMENT
    = actual movement of money between accounts

ALLOCATION
    = who economically bears the cost

CONTRIBUTION
    = what a family member should provide

SETTLEMENT
    = transfer required to restore fair contribution

PLAN
    = future scenario

ACTUAL
    = observed reality

GOAL / SINKING FUND
    = future target funded over time

FORECAST
    = projected future account state

AVAILABLE TO SPEND
    = discretionary amount that can safely be used under a plan
```

Do not collapse these concepts merely to make the database simpler.

---

# 17. User experience goal

The application should not force users to understand financial-model terminology.

The UI should answer practical questions.

Examples:

### Dashboard

```text
Available to spend this month
€620

Plan status
ON TRACK

Lowest projected balance
€1,240

Next important obligation
Life insurance
€960 on 15.03.2027

Vacation goal
€1,400 / €2,400

Shared expenses
A owes B €120
```

### What-if

```text
Current plan:
Available to spend: €620

What if vacation costs €1,000 more?

Available to spend: €410

What if income drops 10%?

Available to spend: €180
```

The technical model should be hidden behind simple user-facing concepts.

---

# 18. Existing product landscape

The project should NOT blindly reinvent an existing product.

Relevant existing products considered:

- Actual Budget
- Firefly III
- Maybe Finance
- Finlynq
- Fricly
- Hearth
- BudgetPilot
- Brisk Budget
- PocketLog
- Gnomeshade
- Flowy
- Kosh
- Savvy

Existing applications cover pieces such as:

- transaction tracking,
- envelope budgeting,
- scheduled transactions,
- recurring transactions,
- goals,
- reports,
- household sharing,
- cash-flow forecasting.

However, the combination that matters here appears less common:

```text
family tenant
+ multiple family users
+ multiple accounts
+ fair shared-cost allocation
+ actual vs plan
+ multiple alternative financial plans
+ future cash-flow simulation
+ sinking funds
+ available-to-spend
+ what-if scenarios
```

The project should therefore focus on this distinctive combination rather than becoming another generic transaction tracker.

---

# 19. User's existing Actual Budget context

The user already uses Actual Budget.

Reported versions:

```text
Client: v26.9.0
Server: v26.9.0
```

A previous observation was that scheduled payments showed the current-month transaction but not the next-month transaction as expected.

Do not assume the new application should replace Actual Budget immediately.

The new project can instead be evaluated as:

1. complementary planning tool,
2. alternative planning engine,
3. learning project,
4. or eventually a complete application.

---

# 20. Architecture guidance

Use clean separation between:

```text
Domain
Application / services
Persistence
API
UI
```

The exact technology stack is not fixed unless the repository already specifies it.

Before making major technology decisions:

1. inspect the existing repository,
2. identify existing conventions,
3. identify build/test/deployment setup,
4. preserve useful existing decisions,
5. avoid introducing unnecessary dependencies.

Prefer boring, maintainable technology.

PostgreSQL is the preferred relational database.

---

# 21. Development principles

## Financial correctness over convenience

Never introduce behavior that can silently produce an incorrect balance or forecast.

All money calculations should use decimal/numeric types appropriate for financial calculations.

Avoid floating-point arithmetic for monetary values.

## Explicit semantics

Prefer explicit domain concepts over clever implicit behavior.

## Tenant isolation

Every tenant-owned query must be tenant-safe.

## Test financial behavior

Important calculations need automated tests.

Especially test:

- transfers,
- matched planned/actual transactions,
- unplanned expenses,
- recurring transactions,
- future goals,
- shared allocations,
- account balances,
- negative balance detection,
- available-to-spend,
- scenario comparison.

## Keep the MVP small

Do not build:

- investments,
- stock trading,
- cryptocurrency,
- complex bank aggregation,
- accounting-grade double-entry,
- tax systems,

unless explicitly requested later.

The core product should prove the planning concept first.

---

# 22. Suggested implementation phases

## Phase 1 — Domain foundation

Implement:

- tenant/family
- users
- accounts
- categories
- actual transactions
- planned transactions
- recurring rules

## Phase 2 — Plan engine

Implement:

- plans
- plan-specific future transactions
- plan cloning
- plan activation/selection

## Phase 3 — Goals

Implement:

- goals
- target amount
- target date
- recurring contribution
- hard/soft priority

## Phase 4 — Forecast engine

Implement:

- account cash-flow
- future balance calculation
- minimum projected balance
- negative balance detection

## Phase 5 — Available-to-spend

Implement a deterministic calculation with explicit assumptions.

## Phase 6 — Shared expenses

Implement:

- allocation rules
- contribution calculation
- settlement calculation

## Phase 7 — What-if scenarios

Implement:

- plan cloning
- parameter changes
- comparison of forecast results
- available-to-spend comparison

---

# 23. First MVP scenario

The first end-to-end scenario should be deliberately simple.

Family:

```text
Users:
    Person A
    Person B

Accounts:
    Salary A
    Salary B
    Mortgage
    Savings
```

Monthly income:

```text
A: €2,000
B: €3,000
Total: €5,000
```

Shared expenses:

```text
Mortgage: €1,200
Electricity: €150
Internet: €30
Groceries: €600
```

Income ratio:

```text
A = 40%
B = 60%
```

Goals:

```text
Life insurance:
    €1,200/year
    target date in 12 months

Vacation:
    €2,400
    target date in 8 months
```

The application should be able to show:

```text
current balances
+ expected income
- expected expenses
- goal funding
- future obligations
= forecast

forecast
→ available_to_spend
```

Then simulate:

```text
Vacation becomes €3,500
```

and show the impact.

---

# 24. Definition of success

The application succeeds if a family can answer these questions quickly:

1. How much money do we really have?
2. How much of it is already committed?
3. How much should each family member contribute?
4. Are we currently following the plan?
5. Will we have enough money in three/six/twelve months?
6. How much can we safely spend now?
7. What happens if something changes?
8. Which plan is sustainable?

If the application cannot answer these questions simply, do not add more generic budgeting features.

---

# 25. Instructions for Claude Code

When working on this repository:

### First

Inspect the repository before changing anything.

Determine:

- current language/framework,
- current directory structure,
- existing database migrations,
- existing tests,
- CI/CD,
- Docker setup,
- README,
- environment configuration.

### Then

Produce a short implementation plan before large changes.

For every significant domain change:

- explain the data model,
- explain financial semantics,
- add migrations,
- add tests,
- update documentation where appropriate.

### Never

- invent existing files,
- assume an API exists without checking,
- silently change financial semantics,
- duplicate actual transactions for every plan,
- make plan data tenant-global,
- mix internal transfers with income/expense,
- use floating point for money,
- add special mortgage-only logic when a generic allocation model is appropriate.

### Prefer

- PostgreSQL constraints where appropriate,
- database indexes based on actual query patterns,
- deterministic calculations,
- pure/testable forecasting functions,
- clear domain names,
- explicit status values,
- small incremental commits/changes.

---

# 26. Key architectural question to preserve

The most important design question is:

> Is this application fundamentally a budget tracker, or is it a household financial planning and simulation engine?

The intended answer is:

**It is primarily a household financial planning and simulation engine.**

Budgeting and transaction tracking are supporting capabilities.

The central product loop is:

```text
REALITY
   ↓
PLAN
   ↓
FORECAST
   ↓
AVAILABLE TO SPEND
   ↓
WHAT-IF
   ↓
DECISION
```

Keep the implementation aligned with this loop.
