using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinPlanner.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedExpenseAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "owner_user_id",
                schema: "finplanner",
                table: "account",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "expense_allocation_rule",
                schema: "finplanner",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    family_id = table.Column<long>(type: "bigint", nullable: false),
                    category_id = table.Column<long>(type: "bigint", nullable: true),
                    method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_allocation_rule", x => x.id);
                    table.CheckConstraint("expense_allocation_rule_method_chk", "method IN ('FIXED_PERCENTAGE', 'EQUAL', 'INCOME_RATIO', 'FIXED_AMOUNT')");
                    table.ForeignKey(
                        name: "FK_expense_allocation_rule_category_category_id",
                        column: x => x.category_id,
                        principalSchema: "finplanner",
                        principalTable: "category",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_expense_allocation_rule_family_family_id",
                        column: x => x.family_id,
                        principalSchema: "finplanner",
                        principalTable: "family",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "expense_allocation_share",
                schema: "finplanner",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    family_id = table.Column<long>(type: "bigint", nullable: false),
                    allocation_rule_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    fixed_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_allocation_share", x => x.id);
                    table.CheckConstraint("expense_allocation_share_fixed_amount_chk", "fixed_amount IS NULL OR fixed_amount >= 0");
                    table.CheckConstraint("expense_allocation_share_percentage_chk", "percentage IS NULL OR (percentage >= 0 AND percentage <= 100)");
                    table.ForeignKey(
                        name: "FK_expense_allocation_share_app_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "finplanner",
                        principalTable: "app_user",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_expense_allocation_share_expense_allocation_rule_allocation~",
                        column: x => x.allocation_rule_id,
                        principalSchema: "finplanner",
                        principalTable: "expense_allocation_rule",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_expense_allocation_share_family_family_id",
                        column: x => x.family_id,
                        principalSchema: "finplanner",
                        principalTable: "family",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "idx_account_owner",
                schema: "finplanner",
                table: "account",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_allocation_rule_category_id",
                schema: "finplanner",
                table: "expense_allocation_rule",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "uq_expense_allocation_rule_family_category",
                schema: "finplanner",
                table: "expense_allocation_rule",
                columns: new[] { "family_id", "category_id" },
                unique: true,
                filter: "active AND category_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_expense_allocation_rule_family_default",
                schema: "finplanner",
                table: "expense_allocation_rule",
                column: "family_id",
                unique: true,
                filter: "active AND category_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_expense_allocation_share_rule",
                schema: "finplanner",
                table: "expense_allocation_share",
                column: "allocation_rule_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_allocation_share_family_id",
                schema: "finplanner",
                table: "expense_allocation_share",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_allocation_share_user_id",
                schema: "finplanner",
                table: "expense_allocation_share",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_expense_allocation_share_rule_user",
                schema: "finplanner",
                table: "expense_allocation_share",
                columns: new[] { "allocation_rule_id", "user_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_account_app_user_owner_user_id",
                schema: "finplanner",
                table: "account",
                column: "owner_user_id",
                principalSchema: "finplanner",
                principalTable: "app_user",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_account_app_user_owner_user_id",
                schema: "finplanner",
                table: "account");

            migrationBuilder.DropTable(
                name: "expense_allocation_share",
                schema: "finplanner");

            migrationBuilder.DropTable(
                name: "expense_allocation_rule",
                schema: "finplanner");

            migrationBuilder.DropIndex(
                name: "idx_account_owner",
                schema: "finplanner",
                table: "account");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                schema: "finplanner",
                table: "account");
        }
    }
}
