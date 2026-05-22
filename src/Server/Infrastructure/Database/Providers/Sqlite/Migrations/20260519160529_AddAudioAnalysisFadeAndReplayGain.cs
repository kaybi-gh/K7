using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioAnalysisFadeAndReplayGain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "FadeInDuration",
                table: "AudioAnalysis",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FadeOutDuration",
                table: "AudioAnalysis",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ReplayGainAlbumGain",
                table: "AudioAnalysis",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ReplayGainTrackGain",
                table: "AudioAnalysis",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FadeInDuration",
                table: "AudioAnalysis");

            migrationBuilder.DropColumn(
                name: "FadeOutDuration",
                table: "AudioAnalysis");

            migrationBuilder.DropColumn(
                name: "ReplayGainAlbumGain",
                table: "AudioAnalysis");

            migrationBuilder.DropColumn(
                name: "ReplayGainTrackGain",
                table: "AudioAnalysis");
        }
    }
}
