using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class Transcoding2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Container",
                table: "FileMetadatas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoFileMetadata_Container",
                table: "FileMetadatas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "SupportedContainers",
                table: "Devices",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Container",
                table: "FileMetadatas");

            migrationBuilder.DropColumn(
                name: "VideoFileMetadata_Container",
                table: "FileMetadatas");

            migrationBuilder.DropColumn(
                name: "SupportedContainers",
                table: "Devices");
        }
    }
}
