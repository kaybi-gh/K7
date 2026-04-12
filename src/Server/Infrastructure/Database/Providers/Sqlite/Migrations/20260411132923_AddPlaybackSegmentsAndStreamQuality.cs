using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybackSegmentsAndStreamQuality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                table: "MediaPlaybackSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceId",
                table: "MediaPlaybackSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "State",
                table: "MediaPlaybackSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StoppedAt",
                table: "MediaPlaybackSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WatchedDurationSeconds",
                table: "MediaPlaybackSessions",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "PlaybackSessionDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaPlaybackSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsTranscode = table.Column<bool>(type: "INTEGER", nullable: true),
                    VideoDecision = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    AudioDecision = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Bitrate = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceVideoCodec = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    SourceAudioCodec = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    SourceVideoWidth = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceVideoHeight = table.Column<int>(type: "INTEGER", nullable: true),
                    StreamVideoCodec = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    StreamAudioCodec = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybackSessionDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaybackSessionDetails_MediaPlaybackSessions_MediaPlaybackSessionId",
                        column: x => x.MediaPlaybackSessionId,
                        principalTable: "MediaPlaybackSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_DeviceId",
                table: "MediaPlaybackSessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_ReferenceId",
                table: "MediaPlaybackSessions",
                column: "ReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_UserId_StartedAt",
                table: "MediaPlaybackSessions",
                columns: new[] { "UserId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackSessionDetails_MediaPlaybackSessionId",
                table: "PlaybackSessionDetails",
                column: "MediaPlaybackSessionId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaPlaybackSessions_Devices_DeviceId",
                table: "MediaPlaybackSessions",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaPlaybackSessions_Devices_DeviceId",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropTable(
                name: "PlaybackSessionDetails");

            migrationBuilder.DropIndex(
                name: "IX_MediaPlaybackSessions_DeviceId",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropIndex(
                name: "IX_MediaPlaybackSessions_ReferenceId",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropIndex(
                name: "IX_MediaPlaybackSessions_UserId_StartedAt",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "State",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "StoppedAt",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "WatchedDurationSeconds",
                table: "MediaPlaybackSessions");
        }
    }
}
