using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UnifyRuleEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Playlists (SmartPlaylist TPH) ---
            migrationBuilder.AddColumn<string>(
                name: "RuleFilter",
                table: "Playlists",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Playlists"
                SET "RuleFilter" = json_object(
                    'matchCondition', COALESCE("MatchCondition", 0),
                    'items', COALESCE(
                        (SELECT json_group_array(
                            json_object(
                                '$type', 'rule',
                                'field', CASE CAST(json_extract(j.value, '$.Field') AS INTEGER)
                                    WHEN 0 THEN 'Title'
                                    WHEN 1 THEN 'Genre'
                                    WHEN 2 THEN 'Year'
                                    WHEN 3 THEN 'Rating'
                                    WHEN 4 THEN 'PlayCount'
                                    WHEN 5 THEN 'DateAdded'
                                    WHEN 6 THEN 'LastPlayed'
                                    WHEN 7 THEN 'IsCompleted'
                                    WHEN 8 THEN 'ArtistName'
                                    WHEN 9 THEN 'AlbumTitle'
                                    WHEN 10 THEN 'TrackNumber'
                                    WHEN 11 THEN 'DiscNumber'
                                    WHEN 12 THEN 'Bpm'
                                    WHEN 13 THEN 'Duration'
                                    WHEN 14 THEN 'OriginalLanguage'
                                    ELSE 'Title'
                                END,
                                'operator', CASE CAST(json_extract(j.value, '$.Operator') AS INTEGER)
                                    WHEN 0 THEN 0
                                    WHEN 1 THEN 1
                                    WHEN 2 THEN 2
                                    WHEN 3 THEN 4
                                    WHEN 4 THEN 5
                                    WHEN 5 THEN 6
                                    WHEN 6 THEN 7
                                    WHEN 7 THEN 10
                                    WHEN 8 THEN 11
                                    WHEN 9 THEN 12
                                    ELSE 0
                                END,
                                'value', json_extract(j.value, '$.Value')
                            )
                        ) FROM json_each("Rules") AS j),
                        json('[]')
                    )
                )
                WHERE "Discriminator" = 'SmartPlaylist' AND "Rules" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "Rules",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "MatchCondition",
                table: "Playlists");

            // --- ContentRestrictionProfiles ---
            migrationBuilder.AddColumn<string>(
                name: "RuleFilter",
                table: "ContentRestrictionProfiles",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "ContentRestrictionProfiles"
                SET "RuleFilter" = json_object(
                    'matchCondition', CASE "MatchCondition"
                        WHEN 0 THEN 1
                        WHEN 1 THEN 0
                        ELSE 1
                    END,
                    'items', COALESCE(
                        (SELECT json_group_array(
                            json_object(
                                '$type', 'rule',
                                'field', CASE CAST(json_extract(j.value, '$.Field') AS INTEGER)
                                    WHEN 0 THEN 'Genre'
                                    WHEN 1 THEN 'ContentRating'
                                    WHEN 2 THEN 'ReleaseYear'
                                    ELSE 'Genre'
                                END,
                                'operator', CASE CAST(json_extract(j.value, '$.Operator') AS INTEGER)
                                    WHEN 0 THEN 0
                                    WHEN 1 THEN 1
                                    WHEN 2 THEN 2
                                    WHEN 3 THEN 3
                                    WHEN 4 THEN 4
                                    WHEN 5 THEN 5
                                    WHEN 6 THEN 6
                                    WHEN 7 THEN 7
                                    WHEN 8 THEN 11
                                    WHEN 9 THEN 12
                                    ELSE 0
                                END,
                                'value', json_extract(j.value, '$.Value')
                            )
                        ) FROM json_each("Rules") AS j),
                        json('[]')
                    )
                );
                """);

            migrationBuilder.AlterColumn<string>(
                name: "RuleFilter",
                table: "ContentRestrictionProfiles",
                type: "jsonb",
                nullable: false,
                defaultValue: "{\"matchCondition\":0,\"items\":[]}",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Rules",
                table: "ContentRestrictionProfiles");

            migrationBuilder.DropColumn(
                name: "MatchCondition",
                table: "ContentRestrictionProfiles");

            // --- NotificationRules (create table) ---
            migrationBuilder.CreateTable(
                name: "NotificationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProviderType = table.Column<int>(type: "INTEGER", nullable: false),
                    PayloadFormat = table.Column<int>(type: "INTEGER", nullable: false),
                    EventTypeNames = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderConfig = table.Column<string>(type: "TEXT", nullable: false),
                    TitleTemplate = table.Column<string>(type: "TEXT", nullable: true),
                    BodyTemplate = table.Column<string>(type: "TEXT", nullable: true),
                    RawJsonTemplate = table.Column<string>(type: "TEXT", nullable: true),
                    RuleFilter = table.Column<string>(type: "jsonb", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRules_IsEnabled",
                table: "NotificationRules",
                column: "IsEnabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // --- Playlists ---
            migrationBuilder.AddColumn<string>(
                name: "Rules",
                table: "Playlists",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatchCondition",
                table: "Playlists",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RuleFilter",
                table: "Playlists");

            // --- ContentRestrictionProfiles ---
            migrationBuilder.AddColumn<string>(
                name: "Rules",
                table: "ContentRestrictionProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatchCondition",
                table: "ContentRestrictionProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "RuleFilter",
                table: "ContentRestrictionProfiles");

            // --- NotificationRules ---
            migrationBuilder.DropTable(
                name: "NotificationRules");
        }
    }
}
