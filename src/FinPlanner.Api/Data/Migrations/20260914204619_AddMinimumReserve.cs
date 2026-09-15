using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinPlanner.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMinimumReserve : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "minimum_reserve",
                schema: "finplanner",
                table: "plan",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "default_minimum_reserve",
                schema: "finplanner",
                table: "family",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "plan_minimum_reserve_chk",
                schema: "finplanner",
                table: "plan",
                sql: "minimum_reserve IS NULL OR minimum_reserve >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "family_default_minimum_reserve_chk",
                schema: "finplanner",
                table: "family",
                sql: "default_minimum_reserve >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "plan_minimum_reserve_chk",
                schema: "finplanner",
                table: "plan");

            migrationBuilder.DropCheckConstraint(
                name: "family_default_minimum_reserve_chk",
                schema: "finplanner",
                table: "family");

            migrationBuilder.DropColumn(
                name: "minimum_reserve",
                schema: "finplanner",
                table: "plan");

            migrationBuilder.DropColumn(
                name: "default_minimum_reserve",
                schema: "finplanner",
                table: "family");
        }
    }
}
