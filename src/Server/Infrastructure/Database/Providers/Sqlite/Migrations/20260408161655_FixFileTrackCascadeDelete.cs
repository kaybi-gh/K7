using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class FixFileTrackCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileTracks_FileMetadatas_AudioFileMetadataId",
                table: "FileTracks");

            migrationBuilder.DropForeignKey(
                name: "FK_FileTracks_FileMetadatas_SubtitleFileTrack_VideoFileMetadataId",
                table: "FileTracks");

            migrationBuilder.DropForeignKey(
                name: "FK_FileTracks_FileMetadatas_VideoFileMetadataId",
                table: "FileTracks");

            migrationBuilder.DropForeignKey(
                name: "FK_FileTracks_FileMetadatas_VideoFileTrack_VideoFileMetadataId",
                table: "FileTracks");

            migrationBuilder.AddForeignKey(
                name: "FK_FileTracks_FileMetadatas_AudioFileMetadataId",
                table: "FileTracks",
                column: "AudioFileMetadataId",
                principalTable: "FileMetadatas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FileTracks_FileMetadatas_SubtitleFileTrack_VideoFileMetadataId",
                table: "FileTracks",
                column: "SubtitleFileTrack_VideoFileMetadataId",
                principalTable: "FileMetadatas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FileTracks_FileMetadatas_VideoFileMetadataId",
                table: "FileTracks",
                column: "VideoFileMetadataId",
                principalTable: "FileMetadatas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FileTracks_FileMetadatas_VideoFileTrack_VideoFileMetadataId",
                table: "FileTracks",
                column: "VideoFileTrack_VideoFileMetadataId",
                principalTable: "FileMetadatas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileTracks_FileMetadatas_AudioFileMetadataId",
                table: "FileTracks");

            migrationBuilder.DropForeignKey(
                name: "FK_FileTracks_FileMetadatas_SubtitleFileTrack_VideoFileMetadataId",
                table: "FileTracks");

            migrationBuilder.DropForeignKey(
                name: "FK_FileTracks_FileMetadatas_VideoFileMetadataId",
                table: "FileTracks");

            migrationBuilder.DropForeignKey(
                name: "FK_FileTracks_FileMetadatas_VideoFileTrack_VideoFileMetadataId",
                table: "FileTracks");

            migrationBuilder.AddForeignKey(
                name: "FK_FileTracks_FileMetadatas_AudioFileMetadataId",
                table: "FileTracks",
                column: "AudioFileMetadataId",
                principalTable: "FileMetadatas",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FileTracks_FileMetadatas_SubtitleFileTrack_VideoFileMetadataId",
                table: "FileTracks",
                column: "SubtitleFileTrack_VideoFileMetadataId",
                principalTable: "FileMetadatas",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FileTracks_FileMetadatas_VideoFileMetadataId",
                table: "FileTracks",
                column: "VideoFileMetadataId",
                principalTable: "FileMetadatas",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FileTracks_FileMetadatas_VideoFileTrack_VideoFileMetadataId",
                table: "FileTracks",
                column: "VideoFileTrack_VideoFileMetadataId",
                principalTable: "FileMetadatas",
                principalColumn: "Id");
        }
    }
}
