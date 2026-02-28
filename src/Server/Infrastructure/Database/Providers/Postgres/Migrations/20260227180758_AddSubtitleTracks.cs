using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddSubtitleTracks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsForced",
                table: "FileTracks",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTextBased",
                table: "FileTracks",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubtitleFileTrack_Codec",
                table: "FileTracks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubtitleFileTrack_Language",
                table: "FileTracks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubtitleFileTrack_Name",
                table: "FileTracks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubtitleFileTrack_VideoFileMetadataId",
                table: "FileTracks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileTracks_SubtitleFileTrack_VideoFileMetadataId",
                table: "FileTracks",
                column: "SubtitleFileTrack_VideoFileMetadataId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileTracks_FileMetadatas_SubtitleFileTrack_VideoFileMetadat~",
                table: "FileTracks",
                column: "SubtitleFileTrack_VideoFileMetadataId",
                principalTable: "FileMetadatas",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileTracks_FileMetadatas_SubtitleFileTrack_VideoFileMetadat~",
                table: "FileTracks");

            migrationBuilder.DropIndex(
                name: "IX_FileTracks_SubtitleFileTrack_VideoFileMetadataId",
                table: "FileTracks");

            migrationBuilder.DropColumn(
                name: "IsForced",
                table: "FileTracks");

            migrationBuilder.DropColumn(
                name: "IsTextBased",
                table: "FileTracks");

            migrationBuilder.DropColumn(
                name: "SubtitleFileTrack_Codec",
                table: "FileTracks");

            migrationBuilder.DropColumn(
                name: "SubtitleFileTrack_Language",
                table: "FileTracks");

            migrationBuilder.DropColumn(
                name: "SubtitleFileTrack_Name",
                table: "FileTracks");

            migrationBuilder.DropColumn(
                name: "SubtitleFileTrack_VideoFileMetadataId",
                table: "FileTracks");
        }
    }
}
