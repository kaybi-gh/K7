using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class EnrichMusicEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Bpm",
                table: "Medias",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscNumber",
                table: "Medias",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LoudnessLufs",
                table: "Medias",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Lyrics",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LyricsLrc",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicAlbum_Overview",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicalKey",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrackNumber",
                table: "Medias",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bpm",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "DiscNumber",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "LoudnessLufs",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Lyrics",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "LyricsLrc",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "MusicAlbum_Overview",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "MusicalKey",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "TrackNumber",
                table: "Medias");
        }
    }
}
