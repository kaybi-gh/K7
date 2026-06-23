using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bpm",
                table: "AudioAnalysis");

            migrationBuilder.DropColumn(
                name: "Danceability",
                table: "AudioAnalysis");

            migrationBuilder.DropColumn(
                name: "Energy",
                table: "AudioAnalysis");

            migrationBuilder.DropColumn(
                name: "LoudnessRange",
                table: "AudioAnalysis");

            migrationBuilder.DropColumn(
                name: "MusicalKey",
                table: "AudioAnalysis");

            migrationBuilder.DropColumn(
                name: "Valence",
                table: "AudioAnalysis");

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    KeyPrefix = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Created = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<string>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys",
                column: "KeyHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.AddColumn<double>(
                name: "Bpm",
                table: "AudioAnalysis",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Danceability",
                table: "AudioAnalysis",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Energy",
                table: "AudioAnalysis",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LoudnessRange",
                table: "AudioAnalysis",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicalKey",
                table: "AudioAnalysis",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Valence",
                table: "AudioAnalysis",
                type: "REAL",
                nullable: true);
        }
    }
}
