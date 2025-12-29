using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpTalk.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceOrderIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrderIndex",
                table: "WorkspaceMembers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrderIndex",
                table: "WorkspaceMembers");
        }
    }
}
