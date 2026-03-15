using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorizationInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Ratings_Users_UserId1",
                table: "Ratings");

            migrationBuilder.DropIndex(
                name: "IX_Ratings_UserId1",
                table: "Ratings");

            migrationBuilder.DropIndex(
                name: "IX_MediaPlaybackSessions_UserId",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "Ratings");

            migrationBuilder.RenameColumn(
                name: "Platform",
                table: "ExternalIds",
                newName: "ProviderName");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Ratings",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DominantColor",
                table: "MetadataPictures",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlaylistId",
                table: "MetadataPictures",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscNumber",
                table: "Medias",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Lyrics",
                table: "Medias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LyricsLrc",
                table: "Medias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicAlbum_Overview",
                table: "Medias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrackNumber",
                table: "Medias",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "MediaPlaybackSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DurationSeconds",
                table: "MediaPlaybackSessions",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PositionSeconds",
                table: "MediaPlaybackSessions",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "Identification_AlbumName",
                table: "IndexedFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identification_ArtistName",
                table: "IndexedFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Identification_TrackNumber",
                table: "IndexedFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AudioAnalysis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MusicTrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChromaprintFingerprint = table.Column<string>(type: "text", nullable: true),
                    ChromaprintDurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    AcoustId = table.Column<string>(type: "TEXT", nullable: true),
                    AcoustIdScore = table.Column<double>(type: "REAL", nullable: true),
                    Bpm = table.Column<double>(type: "REAL", nullable: true),
                    MusicalKey = table.Column<string>(type: "TEXT", nullable: true),
                    LoudnessLufs = table.Column<double>(type: "REAL", nullable: true),
                    LoudnessRange = table.Column<double>(type: "REAL", nullable: true),
                    Energy = table.Column<double>(type: "REAL", nullable: true),
                    Danceability = table.Column<double>(type: "REAL", nullable: true),
                    Valence = table.Column<double>(type: "REAL", nullable: true),
                    WaveformPeaks = table.Column<string>(type: "jsonb", nullable: true),
                    AnalyzedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AnalysisVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioAnalysis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioAnalysis_Medias_MusicTrackId",
                        column: x => x.MusicTrackId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Playlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Discriminator = table.Column<string>(type: "TEXT", maxLength: 13, nullable: false),
                    MediaType = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchCondition = table.Column<int>(type: "INTEGER", nullable: true),
                    Limit = table.Column<int>(type: "INTEGER", nullable: true),
                    OrderBy = table.Column<int>(type: "INTEGER", nullable: true),
                    OrderDescending = table.Column<bool>(type: "INTEGER", nullable: true),
                    LastEvaluatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    Rules = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Playlists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServerSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserCapabilityOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Capability = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCapabilityOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCapabilityOverrides_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlaylistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlaylistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaylistItems_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaylistItems_Playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_UserId",
                table: "Ratings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_PlaylistId",
                table: "MetadataPictures",
                column: "PlaylistId",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_AudioAnalysis_MusicTrackId",
                table: "AudioAnalysis",
                column: "MusicTrackId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_MediaId",
                table: "PlaylistItems",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_PlaylistId_Order",
                table: "PlaylistItems",
                columns: new[] { "PlaylistId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_UserId",
                table: "Playlists",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCapabilityOverrides_UserId",
                table: "UserCapabilityOverrides",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataPictures_Playlists_PlaylistId",
                table: "MetadataPictures",
                column: "PlaylistId",
                principalTable: "Playlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ratings_Users_UserId",
                table: "Ratings",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MetadataPictures_Playlists_PlaylistId",
                table: "MetadataPictures");

            migrationBuilder.DropForeignKey(
                name: "FK_Ratings_Users_UserId",
                table: "Ratings");

            migrationBuilder.DropTable(
                name: "AudioAnalysis");

            migrationBuilder.DropTable(
                name: "PlaylistItems");

            migrationBuilder.DropTable(
                name: "ServerSettings");

            migrationBuilder.DropTable(
                name: "UserCapabilityOverrides");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "Playlists");

            migrationBuilder.DropIndex(
                name: "IX_Ratings_UserId",
                table: "Ratings");

            migrationBuilder.DropIndex(
                name: "IX_MetadataPictures_PlaylistId",
                table: "MetadataPictures");

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
                name: "DominantColor",
                table: "MetadataPictures");

            migrationBuilder.DropColumn(
                name: "PlaylistId",
                table: "MetadataPictures");

            migrationBuilder.DropColumn(
                name: "DiscNumber",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Lyrics",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "LyricsLrc",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "MusicAlbum_Overview",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "TrackNumber",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "PositionSeconds",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropColumn(
                name: "Identification_AlbumName",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "Identification_ArtistName",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "Identification_TrackNumber",
                table: "IndexedFiles");

            migrationBuilder.RenameColumn(
                name: "ProviderName",
                table: "ExternalIds",
                newName: "Platform");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Ratings",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId1",
                table: "Ratings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_UserId1",
                table: "Ratings",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_UserId",
                table: "MediaPlaybackSessions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Ratings_Users_UserId1",
                table: "Ratings",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
