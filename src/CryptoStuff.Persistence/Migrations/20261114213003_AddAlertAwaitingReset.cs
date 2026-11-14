using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoStuff.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertAwaitingReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AwaitingReset",
                table: "PriceAlerts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwaitingReset",
                table: "PriceAlerts");
        }
    }
}
