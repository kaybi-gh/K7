using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddStreamSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StreamSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IndexedFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<double>(type: "double precision", nullable: false),
                    RootDirectory = table.Column<string>(type: "text", nullable: false),
                    PlaybackSettingsJson = table.Column<string>(type: "text", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreamSessions_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StreamSessions_IndexedFiles_IndexedFileId",
                        column: x => x.IndexedFileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StreamSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_DeviceId",
                table: "StreamSessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_IndexedFileId",
                table: "StreamSessions",
                column: "IndexedFileId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_UserId",
                table: "StreamSessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StreamSessions");
        }
    }
}
