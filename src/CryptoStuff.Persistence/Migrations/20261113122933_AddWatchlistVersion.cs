using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoStuff.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchlistVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Watchlists",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "Watchlists");
        }
    }
}
