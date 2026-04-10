using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrogBets.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinancialIntegrityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop existing non-unique index on (MarketId, CreatorId)
            migrationBuilder.DropIndex(
                name: "IX_Bets_MarketId_CreatorId",
                table: "Bets");

            // Create unique filtered index: one pending/active bet per user per market
            // PostgreSQL syntax for filtered index
            migrationBuilder.Sql(
                @"CREATE UNIQUE INDEX ""IX_Bets_MarketId_CreatorId_Unique""
                  ON ""Bets"" (""MarketId"", ""CreatorId"")
                  WHERE ""Status"" IN (0, 1);");  // 0=Pending, 1=Active

            // Add CHECK constraints for non-negative balances
            migrationBuilder.Sql(
                @"ALTER TABLE ""Users""
                  ADD CONSTRAINT ""CK_Users_VirtualBalance_NonNeg""
                  CHECK (""VirtualBalance"" >= 0);");

            migrationBuilder.Sql(
                @"ALTER TABLE ""Users""
                  ADD CONSTRAINT ""CK_Users_ReservedBalance_NonNeg""
                  CHECK (""ReservedBalance"" >= 0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove CHECK constraints
            migrationBuilder.Sql(
                @"ALTER TABLE ""Users""
                  DROP CONSTRAINT IF EXISTS ""CK_Users_VirtualBalance_NonNeg"";");

            migrationBuilder.Sql(
                @"ALTER TABLE ""Users""
                  DROP CONSTRAINT IF EXISTS ""CK_Users_ReservedBalance_NonNeg"";");

            // Drop unique filtered index
            migrationBuilder.DropIndex(
                name: "IX_Bets_MarketId_CreatorId_Unique",
                table: "Bets");

            // Recreate the original non-unique index
            migrationBuilder.CreateIndex(
                name: "IX_Bets_MarketId_CreatorId",
                table: "Bets",
                columns: new[] { "MarketId", "CreatorId" });
        }
    }
}
