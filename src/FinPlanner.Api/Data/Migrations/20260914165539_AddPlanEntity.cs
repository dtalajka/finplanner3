using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinPlanner.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the plan table first so existing rows can be backfilled against it.
            migrationBuilder.CreateTable(
                name: "plan",
                schema: "finplanner",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    family_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan", x => x.id);
                    table.ForeignKey(
                        name: "FK_plan_family_family_id",
                        column: x => x.family_id,
                        principalSchema: "finplanner",
                        principalTable: "family",
                        principalColumn: "id");
                });

            // 2. Give every existing family a default "Normal" plan.
            migrationBuilder.Sql("""
                INSERT INTO finplanner.plan (family_id, name, is_default, is_active, created_at, updated_at)
                SELECT id, 'Normal', true, true, now(), now()
                FROM finplanner.family;
                """);

            // 3. Add plan_id as nullable first so pre-existing rows can be backfilled.
            migrationBuilder.AddColumn<long>(
                name: "plan_id",
                schema: "finplanner",
                table: "recurring_rule",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "plan_id",
                schema: "finplanner",
                table: "planned_transaction",
                type: "bigint",
                nullable: true);

            // 4. Backfill: every existing recurring rule / planned transaction moves to its family's new default plan.
            migrationBuilder.Sql("""
                UPDATE finplanner.recurring_rule AS rr
                SET plan_id = p.id
                FROM finplanner.plan AS p
                WHERE p.family_id = rr.family_id AND p.is_default;
                """);

            migrationBuilder.Sql("""
                UPDATE finplanner.planned_transaction AS pt
                SET plan_id = p.id
                FROM finplanner.plan AS p
                WHERE p.family_id = pt.family_id AND p.is_default;
                """);

            // 5. Now that every row has a plan_id, enforce NOT NULL.
            migrationBuilder.AlterColumn<long>(
                name: "plan_id",
                schema: "finplanner",
                table: "recurring_rule",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "plan_id",
                schema: "finplanner",
                table: "planned_transaction",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_recurring_rule_plan",
                schema: "finplanner",
                table: "recurring_rule",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "idx_planned_transaction_plan",
                schema: "finplanner",
                table: "planned_transaction",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "uq_plan_family_default",
                schema: "finplanner",
                table: "plan",
                column: "family_id",
                unique: true,
                filter: "is_default");

            migrationBuilder.AddForeignKey(
                name: "FK_planned_transaction_plan_plan_id",
                schema: "finplanner",
                table: "planned_transaction",
                column: "plan_id",
                principalSchema: "finplanner",
                principalTable: "plan",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_recurring_rule_plan_plan_id",
                schema: "finplanner",
                table: "recurring_rule",
                column: "plan_id",
                principalSchema: "finplanner",
                principalTable: "plan",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_planned_transaction_plan_plan_id",
                schema: "finplanner",
                table: "planned_transaction");

            migrationBuilder.DropForeignKey(
                name: "FK_recurring_rule_plan_plan_id",
                schema: "finplanner",
                table: "recurring_rule");

            migrationBuilder.DropTable(
                name: "plan",
                schema: "finplanner");

            migrationBuilder.DropIndex(
                name: "idx_recurring_rule_plan",
                schema: "finplanner",
                table: "recurring_rule");

            migrationBuilder.DropIndex(
                name: "idx_planned_transaction_plan",
                schema: "finplanner",
                table: "planned_transaction");

            migrationBuilder.DropColumn(
                name: "plan_id",
                schema: "finplanner",
                table: "recurring_rule");

            migrationBuilder.DropColumn(
                name: "plan_id",
                schema: "finplanner",
                table: "planned_transaction");
        }
    }
}
