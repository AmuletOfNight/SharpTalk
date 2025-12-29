using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpTalk.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeWorkspaceIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Channels_Workspaces_WorkspaceId",
                table: "Channels");

            migrationBuilder.AlterColumn<int>(
                name: "WorkspaceId",
                table: "Channels",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_Channels_Workspaces_WorkspaceId",
                table: "Channels",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Channels_Workspaces_WorkspaceId",
                table: "Channels");

            migrationBuilder.AlterColumn<int>(
                name: "WorkspaceId",
                table: "Channels",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Channels_Workspaces_WorkspaceId",
                table: "Channels",
                column: "WorkspaceId",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
