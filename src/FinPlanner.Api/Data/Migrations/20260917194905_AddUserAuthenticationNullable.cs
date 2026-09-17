using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinPlanner.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAuthenticationNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "finplanner",
                table: "app_user",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                schema: "finplanner",
                table: "app_user",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_app_user_email",
                schema: "finplanner",
                table: "app_user",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_app_user_email",
                schema: "finplanner",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "finplanner",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "password_hash",
                schema: "finplanner",
                table: "app_user");
        }
    }
}
