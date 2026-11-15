using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoStuff.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnicodeCaseInsensitiveNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A data step ahead of the rebuild. EF rebuilds a SQLite table by
            // committing the new table before it recreates the unique index, so
            // two names that only the new collation calls equal would fail the
            // index after the rebuild is already permanent. Building each index
            // under the new collation first, and dropping it again, fails before
            // anything has changed, leaving the database as the earlier release
            // can still open it.
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_Watchlists_Name_Check\" ON \"Watchlists\" (\"Name\" COLLATE UNICODE_NOCASE);");
            migrationBuilder.Sql("DROP INDEX \"IX_Watchlists_Name_Check\";");
            migrationBuilder.Sql("CREATE UNIQUE INDEX \"IX_Portfolios_Name_Check\" ON \"Portfolios\" (\"Name\" COLLATE UNICODE_NOCASE);");
            migrationBuilder.Sql("DROP INDEX \"IX_Portfolios_Name_Check\";");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Watchlists",
                type: "TEXT",
                nullable: false,
                collation: "UNICODE_NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Portfolios",
                type: "TEXT",
                nullable: false,
                collation: "UNICODE_NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldCollation: "NOCASE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Watchlists",
                type: "TEXT",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldCollation: "UNICODE_NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Portfolios",
                type: "TEXT",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldCollation: "UNICODE_NOCASE");
        }
    }
}
