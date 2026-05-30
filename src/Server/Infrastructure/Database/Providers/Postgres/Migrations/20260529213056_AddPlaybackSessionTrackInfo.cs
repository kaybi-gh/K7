using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybackSessionTrackInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudioChannelLayout",
                table: "PlaybackSessionDetails",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudioTrackLanguage",
                table: "PlaybackSessionDetails",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudioTrackTitle",
                table: "PlaybackSessionDetails",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubtitleTrackLanguage",
                table: "PlaybackSessionDetails",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubtitleTrackTitle",
                table: "PlaybackSessionDetails",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioChannelLayout",
                table: "PlaybackSessionDetails");

            migrationBuilder.DropColumn(
                name: "AudioTrackLanguage",
                table: "PlaybackSessionDetails");

            migrationBuilder.DropColumn(
                name: "AudioTrackTitle",
                table: "PlaybackSessionDetails");

            migrationBuilder.DropColumn(
                name: "SubtitleTrackLanguage",
                table: "PlaybackSessionDetails");

            migrationBuilder.DropColumn(
                name: "SubtitleTrackTitle",
                table: "PlaybackSessionDetails");
        }
    }
}
