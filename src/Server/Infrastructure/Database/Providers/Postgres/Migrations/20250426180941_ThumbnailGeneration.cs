using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class ThumbnailGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetadataPictures_VideoFileMetadataId",
                table: "MetadataPictures");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_VideoFileMetadataId",
                table: "MetadataPictures",
                column: "VideoFileMetadataId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MetadataPictures_VideoFileMetadataId",
                table: "MetadataPictures");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_VideoFileMetadataId",
                table: "MetadataPictures",
                column: "VideoFileMetadataId");
        }
    }
}
