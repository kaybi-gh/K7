using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddSerieNumberingAndIdentificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NumberingProviderName",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identification_MusicBrainzAlbumArtistId",
                table: "IndexedFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identification_MusicBrainzArtistId",
                table: "IndexedFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identification_MusicBrainzRecordingId",
                table: "IndexedFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identification_MusicBrainzReleaseGroupId",
                table: "IndexedFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identification_MusicBrainzReleaseId",
                table: "IndexedFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identification_ProviderExternalId",
                table: "IndexedFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identification_ProviderName",
                table: "IndexedFiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumberingProviderName",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Identification_MusicBrainzAlbumArtistId",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "Identification_MusicBrainzArtistId",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "Identification_MusicBrainzRecordingId",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "Identification_MusicBrainzReleaseGroupId",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "Identification_MusicBrainzReleaseId",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "Identification_ProviderExternalId",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "Identification_ProviderName",
                table: "IndexedFiles");
        }
    }
}
