using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryProcessingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IntroDetectionEnabled",
                table: "Libraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "MusicAudioAnalysisEnabled",
                table: "Libraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SeekbarThumbnailGenerationEnabled",
                table: "Libraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "TranscodingEnabled",
                table: "Libraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "TransmuxingEnabled",
                table: "Libraries",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntroDetectionEnabled",
                table: "Libraries");

            migrationBuilder.DropColumn(
                name: "MusicAudioAnalysisEnabled",
                table: "Libraries");

            migrationBuilder.DropColumn(
                name: "SeekbarThumbnailGenerationEnabled",
                table: "Libraries");

            migrationBuilder.DropColumn(
                name: "TranscodingEnabled",
                table: "Libraries");

            migrationBuilder.DropColumn(
                name: "TransmuxingEnabled",
                table: "Libraries");
        }
    }
}
