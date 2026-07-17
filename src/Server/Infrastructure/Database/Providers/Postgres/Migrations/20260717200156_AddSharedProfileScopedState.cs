using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedProfileScopedState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContentRestrictionProfileId",
                table: "SharedProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SharedProfileMediaStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastPlaybackPosition = table.Column<double>(type: "double precision", nullable: false),
                    ProgressPercentage = table.Column<double>(type: "double precision", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    PlayCount = table.Column<int>(type: "integer", nullable: false),
                    LastInteractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastKnownDurationSeconds = table.Column<double>(type: "double precision", nullable: false),
                    ExcludedFromContinueWatching = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedProfileMediaStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedProfileMediaStates_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharedProfileMediaStates_SharedProfiles_SharedProfileId",
                        column: x => x.SharedProfileId,
                        principalTable: "SharedProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SharedProfilePlaylists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaylistId = table.Column<Guid>(type: "uuid", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedProfilePlaylists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedProfilePlaylists_Playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharedProfilePlaylists_SharedProfiles_SharedProfileId",
                        column: x => x.SharedProfileId,
                        principalTable: "SharedProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SharedProfileSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedProfileSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfiles_ContentRestrictionProfileId",
                table: "SharedProfiles",
                column: "ContentRestrictionProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfileMediaStates_MediaId",
                table: "SharedProfileMediaStates",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfileMediaStates_SharedProfileId_IsCompleted_LastInteractedAt",
                table: "SharedProfileMediaStates",
                columns: new[] { "SharedProfileId", "IsCompleted", "LastInteractedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfileMediaStates_SharedProfileId_LastInteractedAt",
                table: "SharedProfileMediaStates",
                columns: new[] { "SharedProfileId", "LastInteractedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfileMediaStates_SharedProfileId_MediaId",
                table: "SharedProfileMediaStates",
                columns: new[] { "SharedProfileId", "MediaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfileMediaStates_SharedProfileId_PlayCount",
                table: "SharedProfileMediaStates",
                columns: new[] { "SharedProfileId", "PlayCount" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfilePlaylists_PlaylistId",
                table: "SharedProfilePlaylists",
                column: "PlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfilePlaylists_SharedProfileId_PlaylistId",
                table: "SharedProfilePlaylists",
                columns: new[] { "SharedProfileId", "PlaylistId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfileSettings_SharedProfileId_Key",
                table: "SharedProfileSettings",
                columns: new[] { "SharedProfileId", "Key" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SharedProfiles_ContentRestrictionProfiles_ContentRestrictio~",
                table: "SharedProfiles",
                column: "ContentRestrictionProfileId",
                principalTable: "ContentRestrictionProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SharedProfiles_ContentRestrictionProfiles_ContentRestrictio~",
                table: "SharedProfiles");

            migrationBuilder.DropTable(
                name: "SharedProfileMediaStates");

            migrationBuilder.DropTable(
                name: "SharedProfilePlaylists");

            migrationBuilder.DropTable(
                name: "SharedProfileSettings");

            migrationBuilder.DropIndex(
                name: "IX_SharedProfiles_ContentRestrictionProfileId",
                table: "SharedProfiles");

            migrationBuilder.DropColumn(
                name: "ContentRestrictionProfileId",
                table: "SharedProfiles");
        }
    }
}
