using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoStuff.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertStateIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PriceAlerts_State",
                table: "PriceAlerts",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PriceAlerts_State",
                table: "PriceAlerts");
        }
    }
}
