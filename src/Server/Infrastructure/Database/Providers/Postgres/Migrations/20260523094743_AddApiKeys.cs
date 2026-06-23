using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
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
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Danceability",
                table: "AudioAnalysis",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Energy",
                table: "AudioAnalysis",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LoudnessRange",
                table: "AudioAnalysis",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicalKey",
                table: "AudioAnalysis",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Valence",
                table: "AudioAnalysis",
                type: "double precision",
                nullable: true);
        }
    }
}
