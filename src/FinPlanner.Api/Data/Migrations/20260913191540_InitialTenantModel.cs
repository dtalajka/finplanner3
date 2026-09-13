using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinPlanner.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialTenantModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finplanner");

            migrationBuilder.CreateTable(
                name: "family",
                schema: "finplanner",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_family", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "account",
                schema: "finplanner",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    family_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    opening_balance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account", x => x.id);
                    table.CheckConstraint("account_currency_chk", "currency ~ '^[A-Z]{3}$'");
                    table.ForeignKey(
                        name: "FK_account_family_family_id",
                        column: x => x.family_id,
                        principalSchema: "finplanner",
                        principalTable: "family",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "app_user",
                schema: "finplanner",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    family_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_user", x => x.id);
                    table.CheckConstraint("app_user_role_chk", "role IN ('OWNER', 'MEMBER')");
                    table.ForeignKey(
                        name: "FK_app_user_family_family_id",
                        column: x => x.family_id,
                        principalSchema: "finplanner",
                        principalTable: "family",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "category",
                schema: "finplanner",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    family_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category", x => x.id);
                    table.CheckConstraint("category_type_chk", "type IN ('INCOME', 'EXPENSE')");
                    table.ForeignKey(
                        name: "FK_category_family_family_id",
                        column: x => x.family_id,
                        principalSchema: "finplanner",
                        principalTable: "family",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "recurring_rule",
                schema: "finplanner",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    family_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    from_account_id = table.Column<long>(type: "bigint", nullable: true),
                    to_account_id = table.Column<long>(type: "bigint", nullable: true),
                    category_id = table.Column<long>(type: "bigint", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    frequency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    day_of_month = table.Column<short>(type: "smallint", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_rule", x => x.id);
                    table.CheckConstraint("recurring_account_chk", "from_account_id IS NOT NULL OR to_account_id IS NOT NULL");
                    table.CheckConstraint("recurring_amount_chk", "amount > 0");
                    table.CheckConstraint("recurring_dates_chk", "end_date IS NULL OR end_date >= start_date");
                    table.CheckConstraint("recurring_day_chk", "day_of_month IS NULL OR day_of_month BETWEEN 1 AND 31");
                    table.CheckConstraint("recurring_frequency_chk", "frequency IN ('DAILY', 'WEEKLY', 'MONTHLY', 'YEARLY')");
                    table.CheckConstraint("recurring_same_account_chk", "from_account_id IS NULL OR to_account_id IS NULL OR from_account_id <> to_account_id");
                    table.CheckConstraint("recurring_transfer_category_chk", "(from_account_id IS NOT NULL AND to_account_id IS NOT NULL AND category_id IS NULL) OR NOT (from_account_id IS NOT NULL AND to_account_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_recurring_rule_account_from_account_id",
                        column: x => x.from_account_id,
                        principalSchema: "finplanner",
                        principalTable: "account",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_recurring_rule_account_to_account_id",
                        column: x => x.to_account_id,
                        principalSchema: "finplanner",
                        principalTable: "account",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_recurring_rule_category_category_id",
                        column: x => x.category_id,
                        principalSchema: "finplanner",
                        principalTable: "category",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_recurring_rule_family_family_id",
                        column: x => x.family_id,
                        principalSchema: "finplanner",
                        principalTable: "family",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "planned_transaction",
                schema: "finplanner",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    family_id = table.Column<long>(type: "bigint", nullable: false),
                    planned_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    from_account_id = table.Column<long>(type: "bigint", nullable: true),
                    to_account_id = table.Column<long>(type: "bigint", nullable: true),
                    category_id = table.Column<long>(type: "bigint", nullable: true),
                    recurring_rule_id = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    cancelled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planned_transaction", x => x.id);
                    table.CheckConstraint("planned_account_chk", "from_account_id IS NOT NULL OR to_account_id IS NOT NULL");
                    table.CheckConstraint("planned_amount_chk", "amount > 0");
                    table.CheckConstraint("planned_same_account_chk", "from_account_id IS NULL OR to_account_id IS NULL OR from_account_id <> to_account_id");
                    table.CheckConstraint("planned_transfer_category_chk", "(from_account_id IS NOT NULL AND to_account_id IS NOT NULL AND category_id IS NULL) OR NOT (from_account_id IS NOT NULL AND to_account_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_planned_transaction_account_from_account_id",
                        column: x => x.from_account_id,
                        principalSchema: "finplanner",
                        principalTable: "account",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_planned_transaction_account_to_account_id",
                        column: x => x.to_account_id,
                        principalSchema: "finplanner",
                        principalTable: "account",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_planned_transaction_category_category_id",
                        column: x => x.category_id,
                        principalSchema: "finplanner",
                        principalTable: "category",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_planned_transaction_family_family_id",
                        column: x => x.family_id,
                        principalSchema: "finplanner",
                        principalTable: "family",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_planned_transaction_recurring_rule_recurring_rule_id",
                        column: x => x.recurring_rule_id,
                        principalSchema: "finplanner",
                        principalTable: "recurring_rule",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "actual_transaction",
                schema: "finplanner",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    family_id = table.Column<long>(type: "bigint", nullable: false),
                    actual_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    from_account_id = table.Column<long>(type: "bigint", nullable: true),
                    to_account_id = table.Column<long>(type: "bigint", nullable: true),
                    category_id = table.Column<long>(type: "bigint", nullable: true),
                    planned_transaction_id = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_actual_transaction", x => x.id);
                    table.CheckConstraint("actual_account_chk", "from_account_id IS NOT NULL OR to_account_id IS NOT NULL");
                    table.CheckConstraint("actual_amount_chk", "amount > 0");
                    table.CheckConstraint("actual_same_account_chk", "from_account_id IS NULL OR to_account_id IS NULL OR from_account_id <> to_account_id");
                    table.CheckConstraint("actual_transfer_category_chk", "(from_account_id IS NOT NULL AND to_account_id IS NOT NULL AND category_id IS NULL) OR NOT (from_account_id IS NOT NULL AND to_account_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_actual_transaction_account_from_account_id",
                        column: x => x.from_account_id,
                        principalSchema: "finplanner",
                        principalTable: "account",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_actual_transaction_account_to_account_id",
                        column: x => x.to_account_id,
                        principalSchema: "finplanner",
                        principalTable: "account",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_actual_transaction_category_category_id",
                        column: x => x.category_id,
                        principalSchema: "finplanner",
                        principalTable: "category",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_actual_transaction_family_family_id",
                        column: x => x.family_id,
                        principalSchema: "finplanner",
                        principalTable: "family",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_actual_transaction_planned_transaction_planned_transaction_~",
                        column: x => x.planned_transaction_id,
                        principalSchema: "finplanner",
                        principalTable: "planned_transaction",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "idx_account_family",
                schema: "finplanner",
                table: "account",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "idx_actual_transaction_family",
                schema: "finplanner",
                table: "actual_transaction",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "IX_actual_transaction_category_id",
                schema: "finplanner",
                table: "actual_transaction",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_actual_transaction_from_account_id",
                schema: "finplanner",
                table: "actual_transaction",
                column: "from_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_actual_transaction_planned_transaction_id",
                schema: "finplanner",
                table: "actual_transaction",
                column: "planned_transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_actual_transaction_to_account_id",
                schema: "finplanner",
                table: "actual_transaction",
                column: "to_account_id");

            migrationBuilder.CreateIndex(
                name: "idx_app_user_family",
                schema: "finplanner",
                table: "app_user",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "category_name_uq",
                schema: "finplanner",
                table: "category",
                columns: new[] { "family_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_planned_transaction_family",
                schema: "finplanner",
                table: "planned_transaction",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "IX_planned_transaction_category_id",
                schema: "finplanner",
                table: "planned_transaction",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_planned_transaction_from_account_id",
                schema: "finplanner",
                table: "planned_transaction",
                column: "from_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_planned_transaction_recurring_rule_id",
                schema: "finplanner",
                table: "planned_transaction",
                column: "recurring_rule_id");

            migrationBuilder.CreateIndex(
                name: "IX_planned_transaction_to_account_id",
                schema: "finplanner",
                table: "planned_transaction",
                column: "to_account_id");

            migrationBuilder.CreateIndex(
                name: "idx_recurring_rule_family",
                schema: "finplanner",
                table: "recurring_rule",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_rule_category_id",
                schema: "finplanner",
                table: "recurring_rule",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_rule_from_account_id",
                schema: "finplanner",
                table: "recurring_rule",
                column: "from_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_rule_to_account_id",
                schema: "finplanner",
                table: "recurring_rule",
                column: "to_account_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "actual_transaction",
                schema: "finplanner");

            migrationBuilder.DropTable(
                name: "app_user",
                schema: "finplanner");

            migrationBuilder.DropTable(
                name: "planned_transaction",
                schema: "finplanner");

            migrationBuilder.DropTable(
                name: "recurring_rule",
                schema: "finplanner");

            migrationBuilder.DropTable(
                name: "account",
                schema: "finplanner");

            migrationBuilder.DropTable(
                name: "category",
                schema: "finplanner");

            migrationBuilder.DropTable(
                name: "family",
                schema: "finplanner");
        }
    }
}
