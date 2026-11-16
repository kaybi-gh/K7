using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RootMediaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ExpiresAt = table.Column<string>(type: "TEXT", nullable: true),
                    MaxAccessCount = table.Column<int>(type: "INTEGER", nullable: true),
                    AccessCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AllowPlayback = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowDownload = table.Column<bool>(type: "INTEGER", nullable: false),
                    CloseWhenConsumed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConsumedAt = table.Column<string>(type: "TEXT", nullable: true),
                    RevokedAt = table.Column<string>(type: "TEXT", nullable: true),
                    LastAccessedAt = table.Column<string>(type: "TEXT", nullable: true),
                    FailedPasswordAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    PasswordLockedUntil = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Created = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<string>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaShares_Medias_RootMediaId",
                        column: x => x.RootMediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaShares_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaShareSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShareId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<string>(type: "TEXT", nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaShareSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaShareSessions_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MediaShareSessions_MediaShares_ShareId",
                        column: x => x.ShareId,
                        principalTable: "MediaShares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaShares_CreatedByUserId",
                table: "MediaShares",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaShares_ExpiresAt",
                table: "MediaShares",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_MediaShares_RootMediaId",
                table: "MediaShares",
                column: "RootMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaShares_TokenHash",
                table: "MediaShares",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaShareSessions_DeviceId",
                table: "MediaShareSessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaShareSessions_ExpiresAt",
                table: "MediaShareSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_MediaShareSessions_ShareId",
                table: "MediaShareSessions",
                column: "ShareId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaShareSessions_TokenHash",
                table: "MediaShareSessions",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaShareSessions");

            migrationBuilder.DropTable(
                name: "MediaShares");
        }
    }
}
