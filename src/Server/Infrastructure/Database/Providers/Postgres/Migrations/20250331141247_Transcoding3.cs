using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class Transcoding3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VideoFileMetadata_Container",
                table: "FileMetadatas");

            migrationBuilder.DropColumn(
                name: "SupportedAudioCodecs",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "SupportedContainers",
                table: "Devices");

            migrationBuilder.RenameColumn(
                name: "VideoFileTrack_CodecName",
                table: "FileTracks",
                newName: "VideoFileTrack_Codec");

            migrationBuilder.RenameColumn(
                name: "CodecName",
                table: "FileTracks",
                newName: "Codec");

            migrationBuilder.RenameColumn(
                name: "SupportedVideoCodecs",
                table: "Devices",
                newName: "SupportedMediaFormatIds");

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "Duration",
                table: "FileMetadatas",
                type: "interval",
                nullable: true,
                oldClrType: typeof(TimeSpan),
                oldType: "interval");

            migrationBuilder.AlterColumn<string>(
                name: "Container",
                table: "FileMetadatas",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "VideoFileMetadata_Duration",
                table: "FileMetadatas",
                type: "interval",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VideoFileMetadata_Duration",
                table: "FileMetadatas");

            migrationBuilder.RenameColumn(
                name: "VideoFileTrack_Codec",
                table: "FileTracks",
                newName: "VideoFileTrack_CodecName");

            migrationBuilder.RenameColumn(
                name: "Codec",
                table: "FileTracks",
                newName: "CodecName");

            migrationBuilder.RenameColumn(
                name: "SupportedMediaFormatIds",
                table: "Devices",
                newName: "SupportedVideoCodecs");

            migrationBuilder.AlterColumn<TimeSpan>(
                name: "Duration",
                table: "FileMetadatas",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0),
                oldClrType: typeof(TimeSpan),
                oldType: "interval",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Container",
                table: "FileMetadatas",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "VideoFileMetadata_Container",
                table: "FileMetadatas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "SupportedAudioCodecs",
                table: "Devices",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "SupportedContainers",
                table: "Devices",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }
    }
}
