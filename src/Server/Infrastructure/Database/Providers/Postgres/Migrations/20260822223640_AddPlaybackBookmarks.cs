using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations;

/// <inheritdoc />
public partial class AddPlaybackBookmarks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PlaybackBookmarks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                SharedProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                Kind = table.Column<int>(type: "integer", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                MediaId = table.Column<Guid>(type: "uuid", nullable: true),
                PositionSeconds = table.Column<double>(type: "double precision", nullable: true),
                DurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                SerieId = table.Column<Guid>(type: "uuid", nullable: true),
                LastCompletedEpisodeId = table.Column<Guid>(type: "uuid", nullable: true),
                NextEpisodeId = table.Column<Guid>(type: "uuid", nullable: true),
                ActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                NextEpisodeAvailableAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "text", nullable: true),
                LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastModifiedBy = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlaybackBookmarks", x => x.Id);
                table.ForeignKey(
                    name: "FK_PlaybackBookmarks_Medias_LastCompletedEpisodeId",
                    column: x => x.LastCompletedEpisodeId,
                    principalTable: "Medias",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PlaybackBookmarks_Medias_MediaId",
                    column: x => x.MediaId,
                    principalTable: "Medias",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PlaybackBookmarks_Medias_NextEpisodeId",
                    column: x => x.NextEpisodeId,
                    principalTable: "Medias",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_PlaybackBookmarks_Medias_SerieId",
                    column: x => x.SerieId,
                    principalTable: "Medias",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PlaybackBookmarks_SharedProfiles_SharedProfileId",
                    column: x => x.SharedProfileId,
                    principalTable: "SharedProfiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PlaybackBookmarks_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackBookmarks_LastCompletedEpisodeId",
            table: "PlaybackBookmarks",
            column: "LastCompletedEpisodeId");

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackBookmarks_MediaId_Item",
            table: "PlaybackBookmarks",
            column: "MediaId");

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackBookmarks_NextEpisodeId_Series",
            table: "PlaybackBookmarks",
            column: "NextEpisodeId");

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackBookmarks_SerieId_Series",
            table: "PlaybackBookmarks",
            column: "SerieId");

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackBookmarks_SharedProfileId",
            table: "PlaybackBookmarks",
            column: "SharedProfileId");

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackBookmarks_SharedProfileId_ActivityAt",
            table: "PlaybackBookmarks",
            columns: new[] { "SharedProfileId", "ActivityAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackBookmarks_SharedProfileId_Kind_UpdatedAt",
            table: "PlaybackBookmarks",
            columns: new[] { "SharedProfileId", "Kind", "UpdatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackBookmarks_SharedProfileId_MediaId_Item",
            table: "PlaybackBookmarks",
            columns: new[] { "SharedProfileId", "MediaId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackBookmarks_SharedProfileId_NextEpisodeAvailableAt",
            table: "PlaybackBookmarks",
            columns: new[] { "SharedProfileId", "NextEpisodeAvailableAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackBookmarks_SharedProfileId_SerieId_Series",
            table: "PlaybackBookmarks",
            columns: new[] { "SharedProfileId", "SerieId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackBookmarks_UserId",
            table: "PlaybackBookmarks",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackBookmarks_UserId_ActivityAt",
            table: "PlaybackBookmarks",
            columns: new[] { "UserId", "ActivityAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackBookmarks_UserId_Kind_UpdatedAt",
            table: "PlaybackBookmarks",
            columns: new[] { "UserId", "Kind", "UpdatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackBookmarks_UserId_MediaId_Item",
            table: "PlaybackBookmarks",
            columns: new[] { "UserId", "MediaId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackBookmarks_UserId_NextEpisodeAvailableAt",
            table: "PlaybackBookmarks",
            columns: new[] { "UserId", "NextEpisodeAvailableAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PlaybackBookmarks_UserId_SerieId_Series",
            table: "PlaybackBookmarks",
            columns: new[] { "UserId", "SerieId" },
            unique: true);

        migrationBuilder.Sql("""
            INSERT INTO "PlaybackBookmarks" (
                "Id", "UserId", "SharedProfileId", "Kind", "UpdatedAt", "MediaId", "PositionSeconds", "DurationSeconds",
                "SerieId", "LastCompletedEpisodeId", "NextEpisodeId", "ActivityAt", "NextEpisodeAvailableAt",
                "Created", "CreatedBy", "LastModified", "LastModifiedBy")
            SELECT
                gen_random_uuid(),
                "UserId",
                NULL,
                0,
                COALESCE("LastInteractedAt", "LastModified"),
                "MediaId",
                "LastPlaybackPosition",
                CASE
                    WHEN "LastKnownDurationSeconds" > 0 THEN "LastKnownDurationSeconds"
                    WHEN "ProgressPercentage" > 0 THEN "LastPlaybackPosition" / ("ProgressPercentage" / 100.0)
                    ELSE 0
                END,
                NULL, NULL, NULL, NULL, '-infinity',
                "Created", "CreatedBy", "LastModified", "LastModifiedBy"
            FROM "UserMediaStates"
            WHERE "ExcludedFromContinueWatching" = FALSE
              AND "IsCompleted" = FALSE
              AND ("LastPlaybackPosition" > 0 OR "ProgressPercentage" > 0);
            """);

        migrationBuilder.Sql("""
            INSERT INTO "PlaybackBookmarks" (
                "Id", "UserId", "SharedProfileId", "Kind", "UpdatedAt", "MediaId", "PositionSeconds", "DurationSeconds",
                "SerieId", "LastCompletedEpisodeId", "NextEpisodeId", "ActivityAt", "NextEpisodeAvailableAt",
                "Created", "CreatedBy", "LastModified", "LastModifiedBy")
            SELECT
                gen_random_uuid(),
                NULL,
                "SharedProfileId",
                0,
                COALESCE("LastInteractedAt", "LastModified"),
                "MediaId",
                "LastPlaybackPosition",
                CASE
                    WHEN "LastKnownDurationSeconds" > 0 THEN "LastKnownDurationSeconds"
                    WHEN "ProgressPercentage" > 0 THEN "LastPlaybackPosition" / ("ProgressPercentage" / 100.0)
                    ELSE 0
                END,
                NULL, NULL, NULL, NULL, '-infinity',
                "Created", "CreatedBy", "LastModified", "LastModifiedBy"
            FROM "SharedProfileMediaStates"
            WHERE "ExcludedFromContinueWatching" = FALSE
              AND "IsCompleted" = FALSE
              AND ("LastPlaybackPosition" > 0 OR "ProgressPercentage" > 0);
            """);

        migrationBuilder.Sql("""
            INSERT INTO "PlaybackBookmarks" (
                "Id", "UserId", "SharedProfileId", "Kind", "UpdatedAt", "MediaId", "PositionSeconds", "DurationSeconds",
                "SerieId", "LastCompletedEpisodeId", "NextEpisodeId", "ActivityAt", "NextEpisodeAvailableAt",
                "Created", "CreatedBy", "LastModified", "LastModifiedBy")
            SELECT
                gen_random_uuid(),
                completed."UserId",
                NULL,
                1,
                completed."ActivityAt",
                NULL,
                NULL,
                NULL,
                completed."SerieId",
                completed."LastCompletedEpisodeId",
                NULL,
                completed."ActivityAt",
                '-infinity',
                completed."Created",
                completed."CreatedBy",
                completed."LastModified",
                completed."LastModifiedBy"
            FROM (
                SELECT
                    ums."UserId",
                    episode."SerieId",
                    ums."MediaId" AS "LastCompletedEpisodeId",
                    COALESCE(ums."LastInteractedAt", ums."LastModified") AS "ActivityAt",
                    ums."Created",
                    ums."CreatedBy",
                    ums."LastModified",
                    ums."LastModifiedBy",
                    ROW_NUMBER() OVER (
                        PARTITION BY ums."UserId", episode."SerieId"
                        ORDER BY
                            CASE WHEN season."SeasonNumber" = 0 THEN -2147483648 ELSE season."SeasonNumber" END DESC,
                            episode."EpisodeNumber" DESC) AS "RowNum"
                FROM "UserMediaStates" ums
                INNER JOIN "Medias" episode ON episode."Id" = ums."MediaId" AND episode."Type" = 5
                INNER JOIN "Medias" season ON season."Id" = episode."SeasonId" AND season."Type" = 6
                WHERE ums."ExcludedFromContinueWatching" = FALSE
                  AND ums."IsCompleted" = TRUE
            ) completed
            WHERE completed."RowNum" = 1;
            """);

        migrationBuilder.Sql("""
            INSERT INTO "PlaybackBookmarks" (
                "Id", "UserId", "SharedProfileId", "Kind", "UpdatedAt", "MediaId", "PositionSeconds", "DurationSeconds",
                "SerieId", "LastCompletedEpisodeId", "NextEpisodeId", "ActivityAt", "NextEpisodeAvailableAt",
                "Created", "CreatedBy", "LastModified", "LastModifiedBy")
            SELECT
                gen_random_uuid(),
                NULL,
                completed."SharedProfileId",
                1,
                completed."ActivityAt",
                NULL,
                NULL,
                NULL,
                completed."SerieId",
                completed."LastCompletedEpisodeId",
                NULL,
                completed."ActivityAt",
                '-infinity',
                completed."Created",
                completed."CreatedBy",
                completed."LastModified",
                completed."LastModifiedBy"
            FROM (
                SELECT
                    spms."SharedProfileId",
                    episode."SerieId",
                    spms."MediaId" AS "LastCompletedEpisodeId",
                    COALESCE(spms."LastInteractedAt", spms."LastModified") AS "ActivityAt",
                    spms."Created",
                    spms."CreatedBy",
                    spms."LastModified",
                    spms."LastModifiedBy",
                    ROW_NUMBER() OVER (
                        PARTITION BY spms."SharedProfileId", episode."SerieId"
                        ORDER BY
                            CASE WHEN season."SeasonNumber" = 0 THEN -2147483648 ELSE season."SeasonNumber" END DESC,
                            episode."EpisodeNumber" DESC) AS "RowNum"
                FROM "SharedProfileMediaStates" spms
                INNER JOIN "Medias" episode ON episode."Id" = spms."MediaId" AND episode."Type" = 5
                INNER JOIN "Medias" season ON season."Id" = episode."SeasonId" AND season."Type" = 6
                WHERE spms."ExcludedFromContinueWatching" = FALSE
                  AND spms."IsCompleted" = TRUE
            ) completed
            WHERE completed."RowNum" = 1;
            """);

        migrationBuilder.DropColumn(
            name: "ExcludedFromContinueWatching",
            table: "UserMediaStates");

        migrationBuilder.DropColumn(
            name: "LastKnownDurationSeconds",
            table: "UserMediaStates");

        migrationBuilder.DropColumn(
            name: "LastPlaybackPosition",
            table: "UserMediaStates");

        migrationBuilder.DropColumn(
            name: "ProgressPercentage",
            table: "UserMediaStates");

        migrationBuilder.DropColumn(
            name: "ExcludedFromContinueWatching",
            table: "SharedProfileMediaStates");

        migrationBuilder.DropColumn(
            name: "LastKnownDurationSeconds",
            table: "SharedProfileMediaStates");

        migrationBuilder.DropColumn(
            name: "LastPlaybackPosition",
            table: "SharedProfileMediaStates");

        migrationBuilder.DropColumn(
            name: "ProgressPercentage",
            table: "SharedProfileMediaStates");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PlaybackBookmarks");

        migrationBuilder.AddColumn<bool>(
            name: "ExcludedFromContinueWatching",
            table: "UserMediaStates",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<double>(
            name: "LastKnownDurationSeconds",
            table: "UserMediaStates",
            type: "double precision",
            nullable: false,
            defaultValue: 0.0);

        migrationBuilder.AddColumn<double>(
            name: "LastPlaybackPosition",
            table: "UserMediaStates",
            type: "double precision",
            nullable: false,
            defaultValue: 0.0);

        migrationBuilder.AddColumn<double>(
            name: "ProgressPercentage",
            table: "UserMediaStates",
            type: "double precision",
            nullable: false,
            defaultValue: 0.0);

        migrationBuilder.AddColumn<bool>(
            name: "ExcludedFromContinueWatching",
            table: "SharedProfileMediaStates",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<double>(
            name: "LastKnownDurationSeconds",
            table: "SharedProfileMediaStates",
            type: "double precision",
            nullable: false,
            defaultValue: 0.0);

        migrationBuilder.AddColumn<double>(
            name: "LastPlaybackPosition",
            table: "SharedProfileMediaStates",
            type: "double precision",
            nullable: false,
            defaultValue: 0.0);

        migrationBuilder.AddColumn<double>(
            name: "ProgressPercentage",
            table: "SharedProfileMediaStates",
            type: "double precision",
            nullable: false,
            defaultValue: 0.0);
    }
}
