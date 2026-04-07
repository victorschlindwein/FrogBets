using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrogBets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CS2Teams",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "CS2Teams",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CS2Teams");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "CS2Teams");
        }
    }
}
