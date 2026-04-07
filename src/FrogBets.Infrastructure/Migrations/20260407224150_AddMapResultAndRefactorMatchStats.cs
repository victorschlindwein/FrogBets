using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrogBets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMapResultAndRefactorMatchStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchStats_Games_GameId",
                table: "MatchStats");

            migrationBuilder.DropColumn(
                name: "Rounds",
                table: "MatchStats");

            migrationBuilder.RenameColumn(
                name: "GameId",
                table: "MatchStats",
                newName: "MapResultId");

            migrationBuilder.RenameIndex(
                name: "IX_MatchStats_PlayerId_GameId",
                table: "MatchStats",
                newName: "IX_MatchStats_PlayerId_MapResultId");

            migrationBuilder.RenameIndex(
                name: "IX_MatchStats_GameId",
                table: "MatchStats",
                newName: "IX_MatchStats_MapResultId");

            migrationBuilder.CreateTable(
                name: "MapResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    MapNumber = table.Column<int>(type: "integer", nullable: false),
                    Rounds = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapResults_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MapResults_GameId_MapNumber",
                table: "MapResults",
                columns: new[] { "GameId", "MapNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MatchStats_MapResults_MapResultId",
                table: "MatchStats",
                column: "MapResultId",
                principalTable: "MapResults",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatchStats_MapResults_MapResultId",
                table: "MatchStats");

            migrationBuilder.DropTable(
                name: "MapResults");

            migrationBuilder.RenameColumn(
                name: "MapResultId",
                table: "MatchStats",
                newName: "GameId");

            migrationBuilder.RenameIndex(
                name: "IX_MatchStats_PlayerId_MapResultId",
                table: "MatchStats",
                newName: "IX_MatchStats_PlayerId_GameId");

            migrationBuilder.RenameIndex(
                name: "IX_MatchStats_MapResultId",
                table: "MatchStats",
                newName: "IX_MatchStats_GameId");

            migrationBuilder.AddColumn<int>(
                name: "Rounds",
                table: "MatchStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_MatchStats_Games_GameId",
                table: "MatchStats",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
