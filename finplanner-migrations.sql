DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'finplanner') THEN
        CREATE SCHEMA finplanner;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS finplanner."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'finplanner') THEN
        CREATE SCHEMA finplanner;
    END IF;
END $EF$;

CREATE TABLE finplanner."FamilyAccounts" (
    "Id" uuid NOT NULL,
    "Name" character varying(120) NOT NULL,
    "Type" character varying(30) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_FamilyAccounts" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_FamilyAccounts_Name" ON finplanner."FamilyAccounts" ("Name");

INSERT INTO finplanner."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260906200628_InitialFamilyAccount', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE finplanner."FamilyTransactions" (
    "Id" uuid NOT NULL,
    "FamilyAccountId" uuid NOT NULL,
    "Description" character varying(200) NOT NULL,
    "Amount" numeric(18,2) NOT NULL,
    "Type" character varying(20) NOT NULL,
    "TransactionDate" date NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    "UpdatedAtUtc" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_FamilyTransactions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_FamilyTransactions_FamilyAccounts_FamilyAccountId" FOREIGN KEY ("FamilyAccountId") REFERENCES finplanner."FamilyAccounts" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_FamilyTransactions_FamilyAccountId_TransactionDate" ON finplanner."FamilyTransactions" ("FamilyAccountId", "TransactionDate");

CREATE INDEX "IX_FamilyTransactions_Type_TransactionDate" ON finplanner."FamilyTransactions" ("Type", "TransactionDate");

INSERT INTO finplanner."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260907115616_AddFamilyTransactions', '10.0.11');

COMMIT;

