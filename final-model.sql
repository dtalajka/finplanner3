CREATE SCHEMA IF NOT EXISTS finplanner;

-- ============================================================
-- ACCOUNT
-- ============================================================

CREATE TABLE finplanner.account (
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


-- ============================================================
-- CATEGORY
-- ============================================================

CREATE TABLE finplanner.category (
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


-- ============================================================
-- RECURRING RULE
-- ============================================================

CREATE TABLE finplanner.recurring_rule (
    id                  BIGSERIAL PRIMARY KEY,
    name                TEXT NOT NULL,

    from_account_id     BIGINT
        REFERENCES finplanner.account(id),

    to_account_id       BIGINT
        REFERENCES finplanner.account(id),

    category_id         BIGINT
        REFERENCES finplanner.category(id),

    amount              NUMERIC(14,2) NOT NULL,

    frequency           VARCHAR(10) NOT NULL,

    day_of_month        SMALLINT,

    start_date          DATE NOT NULL,
    end_date            DATE,

    active              BOOLEAN NOT NULL DEFAULT TRUE,

    description         TEXT,

    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT recurring_amount_chk
        CHECK (amount > 0),

    CONSTRAINT recurring_frequency_chk
        CHECK (
            frequency IN (
                'DAILY',
                'WEEKLY',
                'MONTHLY',
                'YEARLY'
            )
        ),

    CONSTRAINT recurring_day_chk
        CHECK (
            day_of_month IS NULL
            OR day_of_month BETWEEN 1 AND 31
        ),

    CONSTRAINT recurring_dates_chk
        CHECK (
            end_date IS NULL
            OR end_date >= start_date
        ),

    CONSTRAINT recurring_account_chk
        CHECK (
            from_account_id IS NOT NULL
            OR to_account_id IS NOT NULL
        ),

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


-- ============================================================
-- PLANNED TRANSACTION
-- ============================================================

CREATE TABLE finplanner.planned_transaction (
    id                  BIGSERIAL PRIMARY KEY,

    planned_date        DATE NOT NULL,

    amount              NUMERIC(14,2) NOT NULL,

    from_account_id     BIGINT
        REFERENCES finplanner.account(id),

    to_account_id       BIGINT
        REFERENCES finplanner.account(id),

    category_id         BIGINT
        REFERENCES finplanner.category(id),

    recurring_rule_id   BIGINT
        REFERENCES finplanner.recurring_rule(id),

    description         TEXT,

    cancelled           BOOLEAN NOT NULL DEFAULT FALSE,

    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT planned_amount_chk
        CHECK (amount > 0),

    CONSTRAINT planned_account_chk
        CHECK (
            from_account_id IS NOT NULL
            OR to_account_id IS NOT NULL
        ),

    CONSTRAINT planned_same_account_chk
        CHECK (
            from_account_id IS NULL
            OR to_account_id IS NULL
            OR from_account_id <> to_account_id
        ),

    CONSTRAINT planned_transfer_category_chk
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


-- ============================================================
-- ACTUAL TRANSACTION
-- ============================================================

CREATE TABLE finplanner.actual_transaction (
    id                      BIGSERIAL PRIMARY KEY,

    actual_date             DATE NOT NULL,

    amount                  NUMERIC(14,2) NOT NULL,

    from_account_id         BIGINT
        REFERENCES finplanner.account(id),

    to_account_id           BIGINT
        REFERENCES finplanner.account(id),

    category_id             BIGINT
        REFERENCES finplanner.category(id),

    planned_transaction_id  BIGINT
        REFERENCES finplanner.planned_transaction(id),

    description             TEXT,

    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT actual_amount_chk
        CHECK (amount > 0),

    CONSTRAINT actual_account_chk
        CHECK (
            from_account_id IS NOT NULL
            OR to_account_id IS NOT NULL
        ),

    CONSTRAINT actual_same_account_chk
        CHECK (
            from_account_id IS NULL
            OR to_account_id IS NULL
            OR from_account_id <> to_account_id
        ),

    CONSTRAINT actual_transfer_category_chk
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


-- ============================================================
-- INDEXES
-- ============================================================

CREATE INDEX idx_planned_transaction_date
    ON finplanner.planned_transaction (planned_date);

CREATE INDEX idx_planned_transaction_from_account
    ON finplanner.planned_transaction (from_account_id);

CREATE INDEX idx_planned_transaction_to_account
    ON finplanner.planned_transaction (to_account_id);

CREATE INDEX idx_planned_transaction_recurring_rule
    ON finplanner.planned_transaction (recurring_rule_id);


CREATE INDEX idx_actual_transaction_date
    ON finplanner.actual_transaction (actual_date);

CREATE INDEX idx_actual_transaction_from_account
    ON finplanner.actual_transaction (from_account_id);

CREATE INDEX idx_actual_transaction_to_account
    ON finplanner.actual_transaction (to_account_id);

CREATE INDEX idx_actual_transaction_planned
    ON finplanner.actual_transaction (planned_transaction_id);


-- ============================================================
-- ONE ACTUAL TRANSACTION PER PLANNED TRANSACTION
-- ============================================================

CREATE UNIQUE INDEX uq_actual_transaction_planned
    ON finplanner.actual_transaction (planned_transaction_id)
    WHERE planned_transaction_id IS NOT NULL;


-- ============================================================
-- TRANSACTION TIMELINE
-- ============================================================

CREATE VIEW finplanner.transaction_timeline AS

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
        WHEN COALESCE(
                 p.from_account_id,
                 a.from_account_id
             ) IS NOT NULL
         AND COALESCE(
                 p.to_account_id,
                 a.to_account_id
             ) IS NOT NULL
            THEN 'TRANSFER'

        WHEN COALESCE(
                 p.from_account_id,
                 a.from_account_id
             ) IS NULL
         AND COALESCE(
                 p.to_account_id,
                 a.to_account_id
             ) IS NOT NULL
            THEN 'INCOME'

        WHEN COALESCE(
                 p.from_account_id,
                 a.from_account_id
             ) IS NOT NULL
         AND COALESCE(
                 p.to_account_id,
                 a.to_account_id
             ) IS NULL
            THEN 'EXPENSE'
    END AS transaction_type

FROM finplanner.planned_transaction p

FULL OUTER JOIN finplanner.actual_transaction a
    ON a.planned_transaction_id = p.id;