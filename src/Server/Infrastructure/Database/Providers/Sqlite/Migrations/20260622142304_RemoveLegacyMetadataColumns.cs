using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyMetadataColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentRating",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Genres",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Network",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Serie_ContentRating",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Serie_Studios",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Studios",
                table: "Medias");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentRating",
                table: "Medias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Genres",
                table: "Medias",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Network",
                table: "Medias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Serie_ContentRating",
                table: "Medias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Serie_Studios",
                table: "Medias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Studios",
                table: "Medias",
                type: "TEXT",
                nullable: true);
        }
    }
}
