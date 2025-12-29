using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpTalk.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoOpenLastChannel",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "StartOnHome",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoOpenLastChannel",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "StartOnHome",
                table: "Users");
        }
    }
}
