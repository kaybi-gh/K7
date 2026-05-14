using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryMetadataLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetadataFallbackLanguage",
                table: "Libraries",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<string>(
                name: "MetadataLanguage",
                table: "Libraries",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "fr");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetadataFallbackLanguage",
                table: "Libraries");

            migrationBuilder.DropColumn(
                name: "MetadataLanguage",
                table: "Libraries");
        }
    }
}
