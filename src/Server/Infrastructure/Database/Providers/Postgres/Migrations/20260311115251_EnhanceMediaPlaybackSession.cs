using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceMediaPlaybackSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaPlaybackSessions_UserId",
                table: "MediaPlaybackSessions");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "MediaPlaybackSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DurationSeconds",
                table: "MediaPlaybackSessions",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PositionSeconds",
                table: "MediaPlaybackSessions",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_SessionId",
                table: "MediaPlaybackSessions",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_UserId_CompletedAt",
                table: "MediaPlaybackSessions",
                columns: new[] { "UserId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_UserId_MediaId_CompletedAt",
                table: "MediaPlaybackSessions",
                columns: new[] { "UserId", "MediaId", "CompletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaPlaybackSessions_SessionId",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropIndex(
                name: "IX_MediaPlaybackSessions_UserId_CompletedAt",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropIndex(
                name: "IX_MediaPlaybackSessions_UserId_MediaId_CompletedAt",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "PositionSeconds",
                table: "MediaPlaybackSessions");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_UserId",
                table: "MediaPlaybackSessions",
                column: "UserId");
        }
    }
}
