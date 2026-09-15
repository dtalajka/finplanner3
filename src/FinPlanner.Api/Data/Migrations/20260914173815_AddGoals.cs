using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinPlanner.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "goal_id",
                schema: "finplanner",
                table: "planned_transaction",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "goal_id",
                schema: "finplanner",
                table: "actual_transaction",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "goal",
                schema: "finplanner",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    family_id = table.Column<long>(type: "bigint", nullable: false),
                    plan_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    target_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    target_date = table.Column<DateOnly>(type: "date", nullable: false),
                    priority = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    recurring_rule_id = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goal", x => x.id);
                    table.CheckConstraint("goal_priority_chk", "priority IN ('HARD', 'SOFT')");
                    table.CheckConstraint("goal_target_amount_chk", "target_amount > 0");
                    table.ForeignKey(
                        name: "FK_goal_family_family_id",
                        column: x => x.family_id,
                        principalSchema: "finplanner",
                        principalTable: "family",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_goal_plan_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "finplanner",
                        principalTable: "plan",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_goal_recurring_rule_recurring_rule_id",
                        column: x => x.recurring_rule_id,
                        principalSchema: "finplanner",
                        principalTable: "recurring_rule",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "idx_planned_transaction_goal",
                schema: "finplanner",
                table: "planned_transaction",
                column: "goal_id");

            migrationBuilder.CreateIndex(
                name: "idx_actual_transaction_goal",
                schema: "finplanner",
                table: "actual_transaction",
                column: "goal_id");

            migrationBuilder.CreateIndex(
                name: "idx_goal_family",
                schema: "finplanner",
                table: "goal",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "idx_goal_plan",
                schema: "finplanner",
                table: "goal",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_goal_recurring_rule_id",
                schema: "finplanner",
                table: "goal",
                column: "recurring_rule_id");

            migrationBuilder.AddForeignKey(
                name: "FK_actual_transaction_goal_goal_id",
                schema: "finplanner",
                table: "actual_transaction",
                column: "goal_id",
                principalSchema: "finplanner",
                principalTable: "goal",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_planned_transaction_goal_goal_id",
                schema: "finplanner",
                table: "planned_transaction",
                column: "goal_id",
                principalSchema: "finplanner",
                principalTable: "goal",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_actual_transaction_goal_goal_id",
                schema: "finplanner",
                table: "actual_transaction");

            migrationBuilder.DropForeignKey(
                name: "FK_planned_transaction_goal_goal_id",
                schema: "finplanner",
                table: "planned_transaction");

            migrationBuilder.DropTable(
                name: "goal",
                schema: "finplanner");

            migrationBuilder.DropIndex(
                name: "idx_planned_transaction_goal",
                schema: "finplanner",
                table: "planned_transaction");

            migrationBuilder.DropIndex(
                name: "idx_actual_transaction_goal",
                schema: "finplanner",
                table: "actual_transaction");

            migrationBuilder.DropColumn(
                name: "goal_id",
                schema: "finplanner",
                table: "planned_transaction");

            migrationBuilder.DropColumn(
                name: "goal_id",
                schema: "finplanner",
                table: "actual_transaction");
        }
    }
}
