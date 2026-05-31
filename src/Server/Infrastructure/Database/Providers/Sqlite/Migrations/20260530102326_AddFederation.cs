using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddFederation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PeerServerId",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndedAt",
                table: "StreamSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PeerServerId",
                table: "StreamSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PeerServerId",
                table: "Persons",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PeerServerId",
                table: "Medias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RootPath",
                table: "Libraries",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<Guid>(
                name: "PeerServerId",
                table: "Libraries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PeerRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequesterUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RequesterName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    RespondedAt = table.Column<string>(type: "TEXT", nullable: true),
                    Created = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<string>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeerRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PeerServers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    OutboundClientId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    OutboundClientSecret = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    InboundApplicationId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LastSeen = table.Column<string>(type: "TEXT", nullable: true),
                    Created = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<string>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeerServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PeerShareAgreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PeerServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LibraryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Direction = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MaxConcurrentStreams = table.Column<int>(type: "INTEGER", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SharePlaybackHistory = table.Column<bool>(type: "INTEGER", nullable: false),
                    Created = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<string>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeerShareAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeerShareAgreements_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PeerShareAgreements_PeerServers_PeerServerId",
                        column: x => x.PeerServerId,
                        principalTable: "PeerServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RemoteIndexedFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PeerServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RemoteFileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Extension = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RemoteMediaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LibraryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RemoteLibraryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Created = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<string>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteIndexedFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemoteIndexedFiles_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RemoteIndexedFiles_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RemoteIndexedFiles_PeerServers_PeerServerId",
                        column: x => x.PeerServerId,
                        principalTable: "PeerServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_PeerServerId",
                table: "Users",
                column: "PeerServerId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_PeerServerId",
                table: "StreamSessions",
                column: "PeerServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Persons_PeerServerId",
                table: "Persons",
                column: "PeerServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_PeerServerId",
                table: "Medias",
                column: "PeerServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Libraries_PeerServerId",
                table: "Libraries",
                column: "PeerServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PeerShareAgreements_LibraryId",
                table: "PeerShareAgreements",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_PeerShareAgreements_PeerServerId",
                table: "PeerShareAgreements",
                column: "PeerServerId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteIndexedFiles_LibraryId",
                table: "RemoteIndexedFiles",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteIndexedFiles_MediaId",
                table: "RemoteIndexedFiles",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteIndexedFiles_PeerServerId",
                table: "RemoteIndexedFiles",
                column: "PeerServerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Libraries_PeerServers_PeerServerId",
                table: "Libraries",
                column: "PeerServerId",
                principalTable: "PeerServers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_PeerServers_PeerServerId",
                table: "Medias",
                column: "PeerServerId",
                principalTable: "PeerServers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Persons_PeerServers_PeerServerId",
                table: "Persons",
                column: "PeerServerId",
                principalTable: "PeerServers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StreamSessions_PeerServers_PeerServerId",
                table: "StreamSessions",
                column: "PeerServerId",
                principalTable: "PeerServers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_PeerServers_PeerServerId",
                table: "Users",
                column: "PeerServerId",
                principalTable: "PeerServers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Libraries_PeerServers_PeerServerId",
                table: "Libraries");

            migrationBuilder.DropForeignKey(
                name: "FK_Medias_PeerServers_PeerServerId",
                table: "Medias");

            migrationBuilder.DropForeignKey(
                name: "FK_Persons_PeerServers_PeerServerId",
                table: "Persons");

            migrationBuilder.DropForeignKey(
                name: "FK_StreamSessions_PeerServers_PeerServerId",
                table: "StreamSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_PeerServers_PeerServerId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "PeerRequests");

            migrationBuilder.DropTable(
                name: "PeerShareAgreements");

            migrationBuilder.DropTable(
                name: "RemoteIndexedFiles");

            migrationBuilder.DropTable(
                name: "PeerServers");

            migrationBuilder.DropIndex(
                name: "IX_Users_PeerServerId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_StreamSessions_PeerServerId",
                table: "StreamSessions");

            migrationBuilder.DropIndex(
                name: "IX_Persons_PeerServerId",
                table: "Persons");

            migrationBuilder.DropIndex(
                name: "IX_Medias_PeerServerId",
                table: "Medias");

            migrationBuilder.DropIndex(
                name: "IX_Libraries_PeerServerId",
                table: "Libraries");

            migrationBuilder.DropColumn(
                name: "PeerServerId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "StreamSessions");

            migrationBuilder.DropColumn(
                name: "PeerServerId",
                table: "StreamSessions");

            migrationBuilder.DropColumn(
                name: "PeerServerId",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "PeerServerId",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "PeerServerId",
                table: "Libraries");

            migrationBuilder.AlterColumn<string>(
                name: "RootPath",
                table: "Libraries",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
