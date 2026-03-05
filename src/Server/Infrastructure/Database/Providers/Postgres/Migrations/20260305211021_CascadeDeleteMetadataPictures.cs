using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeleteMetadataPictures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MetadataPictures_FileMetadatas_VideoFileMetadataId",
                table: "MetadataPictures");

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataPictures_FileMetadatas_VideoFileMetadataId",
                table: "MetadataPictures",
                column: "VideoFileMetadataId",
                principalTable: "FileMetadatas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MetadataPictures_FileMetadatas_VideoFileMetadataId",
                table: "MetadataPictures");

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataPictures_FileMetadatas_VideoFileMetadataId",
                table: "MetadataPictures",
                column: "VideoFileMetadataId",
                principalTable: "FileMetadatas",
                principalColumn: "Id");
        }
    }
}
