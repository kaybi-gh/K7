using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartPlaylists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "Playlists",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastEvaluatedAt",
                table: "Playlists",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Limit",
                table: "Playlists",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatchCondition",
                table: "Playlists",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaType",
                table: "Playlists",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderBy",
                table: "Playlists",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OrderDescending",
                table: "Playlists",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rules",
                table: "Playlists",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "LastEvaluatedAt",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "Limit",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "MatchCondition",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "OrderBy",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "OrderDescending",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "Rules",
                table: "Playlists");
        }
    }
}
