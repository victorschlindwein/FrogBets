using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrogBets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerRatingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CS2Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CS2Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CS2Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RealName = table.Column<string>(type: "text", nullable: true),
                    PhotoUrl = table.Column<string>(type: "text", nullable: true),
                    PlayerScore = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    MatchesCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CS2Players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CS2Players_CS2Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "CS2Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatchStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kills = table.Column<int>(type: "integer", nullable: false),
                    Deaths = table.Column<int>(type: "integer", nullable: false),
                    Assists = table.Column<int>(type: "integer", nullable: false),
                    TotalDamage = table.Column<double>(type: "double precision", nullable: false),
                    Rounds = table.Column<int>(type: "integer", nullable: false),
                    KastPercent = table.Column<double>(type: "double precision", nullable: false),
                    Rating = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchStats_CS2Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "CS2Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchStats_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CS2Players_Nickname",
                table: "CS2Players",
                column: "Nickname",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CS2Players_TeamId",
                table: "CS2Players",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_CS2Teams_Name",
                table: "CS2Teams",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchStats_GameId",
                table: "MatchStats",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchStats_PlayerId_GameId",
                table: "MatchStats",
                columns: new[] { "PlayerId", "GameId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchStats");

            migrationBuilder.DropTable(
                name: "CS2Players");

            migrationBuilder.DropTable(
                name: "CS2Teams");
        }
    }
}
