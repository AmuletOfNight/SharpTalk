using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpTalk.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceDescriptionAndCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Workspaces",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Workspaces",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Workspaces");
        }
    }
}
