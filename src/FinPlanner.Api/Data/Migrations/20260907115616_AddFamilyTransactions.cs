using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinPlanner.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FamilyTransactions",
                schema: "finplanner",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyTransactions_FamilyAccounts_FamilyAccountId",
                        column: x => x.FamilyAccountId,
                        principalSchema: "finplanner",
                        principalTable: "FamilyAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyTransactions_FamilyAccountId_TransactionDate",
                schema: "finplanner",
                table: "FamilyTransactions",
                columns: new[] { "FamilyAccountId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyTransactions_Type_TransactionDate",
                schema: "finplanner",
                table: "FamilyTransactions",
                columns: new[] { "Type", "TransactionDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FamilyTransactions",
                schema: "finplanner");
        }
    }
}
