using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class FederationStreamSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StreamSessions_PeerServers_PeerServerId",
                table: "StreamSessions");

            migrationBuilder.AlterColumn<Guid>(
                name: "IndexedFileId",
                table: "StreamSessions",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<Guid>(
                name: "DeviceId",
                table: "StreamSessions",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<Guid>(
                name: "RemoteIndexedFileId",
                table: "StreamSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RemoteSessionId",
                table: "StreamSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_RemoteIndexedFileId",
                table: "StreamSessions",
                column: "RemoteIndexedFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_StreamSessions_PeerServers_PeerServerId",
                table: "StreamSessions",
                column: "PeerServerId",
                principalTable: "PeerServers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StreamSessions_RemoteIndexedFiles_RemoteIndexedFileId",
                table: "StreamSessions",
                column: "RemoteIndexedFileId",
                principalTable: "RemoteIndexedFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StreamSessions_PeerServers_PeerServerId",
                table: "StreamSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_StreamSessions_RemoteIndexedFiles_RemoteIndexedFileId",
                table: "StreamSessions");

            migrationBuilder.DropIndex(
                name: "IX_StreamSessions_RemoteIndexedFileId",
                table: "StreamSessions");

            migrationBuilder.DropColumn(
                name: "RemoteIndexedFileId",
                table: "StreamSessions");

            migrationBuilder.DropColumn(
                name: "RemoteSessionId",
                table: "StreamSessions");

            migrationBuilder.AlterColumn<Guid>(
                name: "IndexedFileId",
                table: "StreamSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DeviceId",
                table: "StreamSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StreamSessions_PeerServers_PeerServerId",
                table: "StreamSessions",
                column: "PeerServerId",
                principalTable: "PeerServers",
                principalColumn: "Id");
        }
    }
}
