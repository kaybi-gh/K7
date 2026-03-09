using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddMusicIdentificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Identification_AlbumName",
                table: "IndexedFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identification_ArtistName",
                table: "IndexedFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Identification_TrackNumber",
                table: "IndexedFiles",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Identification_AlbumName",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "Identification_ArtistName",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "Identification_TrackNumber",
                table: "IndexedFiles");
        }
    }
}
