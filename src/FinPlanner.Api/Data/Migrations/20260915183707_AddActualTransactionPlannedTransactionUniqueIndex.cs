using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinPlanner.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActualTransactionPlannedTransactionUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_actual_transaction_planned_transaction_id",
                schema: "finplanner",
                table: "actual_transaction");

            migrationBuilder.CreateIndex(
                name: "uq_actual_transaction_planned",
                schema: "finplanner",
                table: "actual_transaction",
                column: "planned_transaction_id",
                unique: true,
                filter: "planned_transaction_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_actual_transaction_planned",
                schema: "finplanner",
                table: "actual_transaction");

            migrationBuilder.CreateIndex(
                name: "IX_actual_transaction_planned_transaction_id",
                schema: "finplanner",
                table: "actual_transaction",
                column: "planned_transaction_id");
        }
    }
}
