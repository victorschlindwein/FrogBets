using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrogBets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTeamLeader",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TradeListings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeListings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeListings_CS2Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "CS2Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TradeListings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradeOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposerTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiverTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeOffers_CS2Teams_ProposerTeamId",
                        column: x => x.ProposerTeamId,
                        principalTable: "CS2Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TradeOffers_CS2Teams_ReceiverTeamId",
                        column: x => x.ReceiverTeamId,
                        principalTable: "CS2Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TradeOffers_Users_OfferedUserId",
                        column: x => x.OfferedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TradeOffers_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TeamId",
                table: "Users",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeListings_TeamId",
                table: "TradeListings",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeListings_UserId",
                table: "TradeListings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeOffers_OfferedUserId",
                table: "TradeOffers",
                column: "OfferedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeOffers_ProposerTeamId",
                table: "TradeOffers",
                column: "ProposerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeOffers_ReceiverTeamId",
                table: "TradeOffers",
                column: "ReceiverTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeOffers_TargetUserId",
                table: "TradeOffers",
                column: "TargetUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_CS2Teams_TeamId",
                table: "Users",
                column: "TeamId",
                principalTable: "CS2Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_CS2Teams_TeamId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "TradeListings");

            migrationBuilder.DropTable(
                name: "TradeOffers");

            migrationBuilder.DropIndex(
                name: "IX_Users_TeamId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsTeamLeader",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Users");
        }
    }
}
