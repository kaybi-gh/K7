using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddDiagnosticsColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastMetadataRefreshedAt",
                table: "Medias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MetadataRefreshIntervalDays",
                table: "Libraries",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastMetadataRefreshedAt",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "MetadataRefreshIntervalDays",
                table: "Libraries");
        }
    }
}
