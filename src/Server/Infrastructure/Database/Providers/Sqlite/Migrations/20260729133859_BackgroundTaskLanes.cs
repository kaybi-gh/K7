using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <summary>
    /// Introduces the typed scheduling model: Lane, WorkClass, TriggeredBy, and a Priority that becomes
    /// a dynamic score.
    /// </summary>
    /// <remarks>
    /// The scaffolded version renamed Priority to WorkClass and ConcurrencyGroup to FederationPeerId,
    /// which would have put old 0..6 priorities in a weights column and group names in a Guid column. The
    /// new columns are therefore backfilled explicitly here. Task name is the backfill signal for WorkClass
    /// because it identifies the work precisely, whereas the old Priority mixed kind of work and urgency.
    /// The Priority column itself is kept but reset: it no longer holds a band, only dynamic urgency.
    /// </remarks>
    public partial class BackgroundTaskLanes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BackgroundTasks_ConcurrencyGroup",
                table: "BackgroundTasks");

            migrationBuilder.DropIndex(
                name: "IX_BackgroundTasks_Status_Priority_Created",
                table: "BackgroundTasks");

            migrationBuilder.AddColumn<bool>(
                name: "CancellationRequested",
                table: "BackgroundTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "FederationPeerId",
                table: "BackgroundTasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Lane",
                table: "BackgroundTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<int>(
                name: "WorkClass",
                table: "BackgroundTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<int>(
                name: "ReclaimCount",
                table: "BackgroundTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TriggeredBy",
                table: "BackgroundTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Lane from the former free-form concurrency group.
            migrationBuilder.Sql("""
                UPDATE "BackgroundTasks" SET "Lane" = CASE
                    WHEN "ConcurrencyGroup" = 'library-scan' THEN 0
                    WHEN "ConcurrencyGroup" = 'probe' THEN 1
                    WHEN "ConcurrencyGroup" = 'ffprobe' THEN 1
                    WHEN "ConcurrencyGroup" = 'file-metadata' THEN 1
                    WHEN "ConcurrencyGroup" = 'hls-segments' THEN 2
                    WHEN "ConcurrencyGroup" = 'ffmpeg' THEN 3
                    WHEN "ConcurrencyGroup" = 'image-processing' THEN 5
                    WHEN "ConcurrencyGroup" = 'download-transcode' THEN 8
                    WHEN "ConcurrencyGroup" LIKE 'federation:%' THEN 7
                    ELSE 6
                END;
                """);

            // Peer isolation used to be encoded in the group name. SQLite stores Guid as TEXT, so the
            // substring is assigned as-is once its shape has been checked.
            migrationBuilder.Sql("""
                UPDATE "BackgroundTasks"
                SET "FederationPeerId" = upper(substr("ConcurrencyGroup", 12))
                WHERE "ConcurrencyGroup" LIKE 'federation:%'
                  AND length(substr("ConcurrencyGroup", 12)) = 36;
                """);

            // WorkClass from the task name.
            migrationBuilder.Sql("""
                UPDATE "BackgroundTasks" SET "WorkClass" = CASE
                    WHEN "Name" IN ('CreateMediaCommand', 'BulkCreateMediasCommand', 'IndexLibraryFilesCommand', 'IndexLibraryPathsCommand') THEN 400
                    WHEN "Name" = 'CreateFileMetadatasCommand' THEN 380
                    WHEN "Name" = 'RefreshMediaMetadatasCommand' THEN 300
                    WHEN "Name" IN ('ComputeHlsSegmentsCommand', 'ExtractChaptersCommand') THEN 200
                    ELSE 100
                END;
                """);

            migrationBuilder.DropColumn(
                name: "ConcurrencyGroup",
                table: "BackgroundTasks");

            // Priority keeps its column but changes meaning: it used to be a Lowest..Highest enum mixing
            // kind of work and urgency, and is now a dynamic score raised by boosts. Old values are
            // meaningless under the new semantics, so reset them; WorkClass now carries the band.
            migrationBuilder.Sql("""
                UPDATE "BackgroundTasks" SET "Priority" = 0;
                """);

            // The unique filtered index below would fail on data created before enqueue deduplication
            // was atomic. Keep the oldest active row of each duplicate set.
            migrationBuilder.Sql("""
                DELETE FROM "BackgroundTasks" WHERE "Id" IN (
                    SELECT "Id" FROM (
                        SELECT "Id", ROW_NUMBER() OVER (
                            PARTITION BY "Name", "TargetEntityId" ORDER BY "Created", "Id") AS rn
                        FROM "BackgroundTasks"
                        WHERE "Status" IN (0, 1, 2) AND "TargetEntityId" IS NOT NULL
                    ) duplicates WHERE duplicates.rn > 1
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundTasks_Lane",
                table: "BackgroundTasks",
                column: "Lane");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundTasks_Status_WorkClass_Priority_Created",
                table: "BackgroundTasks",
                columns: new[] { "Status", "WorkClass", "Priority", "Created" });

            migrationBuilder.CreateIndex(
                name: "UX_BackgroundTasks_Name_TargetEntityId_Active",
                table: "BackgroundTasks",
                columns: new[] { "Name", "TargetEntityId" },
                unique: true,
                filter: "\"Status\" IN (0, 1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BackgroundTasks_Lane",
                table: "BackgroundTasks");

            migrationBuilder.DropIndex(
                name: "IX_BackgroundTasks_Status_WorkClass_Priority_Created",
                table: "BackgroundTasks");

            migrationBuilder.DropIndex(
                name: "UX_BackgroundTasks_Name_TargetEntityId_Active",
                table: "BackgroundTasks");

            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyGroup",
                table: "BackgroundTasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "BackgroundTasks" SET "ConcurrencyGroup" = CASE "Lane"
                    WHEN 0 THEN 'library-scan'
                    WHEN 1 THEN 'probe'
                    WHEN 2 THEN 'hls-segments'
                    WHEN 3 THEN 'ffmpeg'
                    WHEN 5 THEN 'image-processing'
                    WHEN 8 THEN 'download-transcode'
                    ELSE NULL
                END;
                """);

            migrationBuilder.Sql("""
                UPDATE "BackgroundTasks" SET "Priority" = CASE
                    WHEN "WorkClass" >= 380 THEN 5
                    WHEN "WorkClass" >= 300 THEN 4
                    WHEN "WorkClass" >= 200 THEN 3
                    ELSE 0
                END;
                """);

            migrationBuilder.DropColumn(
                name: "CancellationRequested",
                table: "BackgroundTasks");

            migrationBuilder.DropColumn(
                name: "FederationPeerId",
                table: "BackgroundTasks");

            migrationBuilder.DropColumn(
                name: "Lane",
                table: "BackgroundTasks");

            migrationBuilder.DropColumn(
                name: "WorkClass",
                table: "BackgroundTasks");

            migrationBuilder.DropColumn(
                name: "ReclaimCount",
                table: "BackgroundTasks");

            migrationBuilder.DropColumn(
                name: "TriggeredBy",
                table: "BackgroundTasks");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundTasks_ConcurrencyGroup",
                table: "BackgroundTasks",
                column: "ConcurrencyGroup");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundTasks_Status_Priority_Created",
                table: "BackgroundTasks",
                columns: new[] { "Status", "Priority", "Created" });
        }
    }
}
