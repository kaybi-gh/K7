using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bpm",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "LoudnessLufs",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "MusicalKey",
                table: "Medias");

            migrationBuilder.CreateTable(
                name: "AudioAnalysis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MusicTrackId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChromaprintFingerprint = table.Column<string>(type: "text", nullable: true),
                    ChromaprintDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    AcoustId = table.Column<string>(type: "text", nullable: true),
                    AcoustIdScore = table.Column<double>(type: "double precision", nullable: true),
                    Bpm = table.Column<double>(type: "double precision", nullable: true),
                    MusicalKey = table.Column<string>(type: "text", nullable: true),
                    LoudnessLufs = table.Column<double>(type: "double precision", nullable: true),
                    LoudnessRange = table.Column<double>(type: "double precision", nullable: true),
                    Energy = table.Column<double>(type: "double precision", nullable: true),
                    Danceability = table.Column<double>(type: "double precision", nullable: true),
                    Valence = table.Column<double>(type: "double precision", nullable: true),
                    AnalyzedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnalysisVersion = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioAnalysis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioAnalysis_Medias_MusicTrackId",
                        column: x => x.MusicTrackId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudioAnalysis_MusicTrackId",
                table: "AudioAnalysis",
                column: "MusicTrackId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudioAnalysis");

            migrationBuilder.AddColumn<double>(
                name: "Bpm",
                table: "Medias",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LoudnessLufs",
                table: "Medias",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicalKey",
                table: "Medias",
                type: "text",
                nullable: true);
        }
    }
}
