using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoStuff.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioLastSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PortfolioTransactions_PortfolioId",
                table: "PortfolioTransactions");

            migrationBuilder.AddColumn<int>(
                name: "LastSequence",
                table: "Portfolios",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // A data step: a stored portfolio keeps no record of a newest
            // transaction removed before this release, so the best its mark
            // can be is the highest sequence it still holds.
            migrationBuilder.Sql(
                "UPDATE \"Portfolios\" SET \"LastSequence\" = COALESCE("
                + "(SELECT MAX(\"Sequence\") FROM \"PortfolioTransactions\" WHERE \"PortfolioId\" = \"Portfolios\".\"Id\"), 0);");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioTransactions_PortfolioId_Sequence",
                table: "PortfolioTransactions",
                columns: new[] { "PortfolioId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PortfolioTransactions_PortfolioId_Sequence",
                table: "PortfolioTransactions");

            migrationBuilder.DropColumn(
                name: "LastSequence",
                table: "Portfolios");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioTransactions_PortfolioId",
                table: "PortfolioTransactions",
                column: "PortfolioId");
        }
    }
}
