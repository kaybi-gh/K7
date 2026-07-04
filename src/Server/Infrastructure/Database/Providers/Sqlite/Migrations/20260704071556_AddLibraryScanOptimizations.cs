using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryScanOptimizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutoScanIntervalHours",
                table: "Libraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<bool>(
                name: "RealtimeMonitorEnabled",
                table: "Libraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "LastWriteTimeUtc",
                table: "IndexedFiles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoScanIntervalHours",
                table: "Libraries");

            migrationBuilder.DropColumn(
                name: "RealtimeMonitorEnabled",
                table: "Libraries");

            migrationBuilder.DropColumn(
                name: "LastWriteTimeUtc",
                table: "IndexedFiles");
        }
    }
}
