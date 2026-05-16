using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMusicArtistMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsGuest",
                table: "PersonRoles",
                newName: "IsActive");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "PersonRoles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArtistId",
                table: "Medias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArtistType",
                table: "Medias",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Biography",
                table: "Medias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Medias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MusicTrack_ArtistId",
                table: "Medias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MusicArtistCredits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MusicArtistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsGuest = table.Column<bool>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: true),
                    MusicAlbumId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicArtistCredits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MusicArtistCredits_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MusicArtistCredits_Medias_MusicAlbumId",
                        column: x => x.MusicAlbumId,
                        principalTable: "Medias",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MusicArtistCredits_Medias_MusicArtistId",
                        column: x => x.MusicArtistId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Medias_ArtistId",
                table: "Medias",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_MusicTrack_ArtistId",
                table: "Medias",
                column: "MusicTrack_ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicArtistCredits_MediaId",
                table: "MusicArtistCredits",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicArtistCredits_MusicAlbumId",
                table: "MusicArtistCredits",
                column: "MusicAlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicArtistCredits_MusicArtistId_MediaId",
                table: "MusicArtistCredits",
                columns: new[] { "MusicArtistId", "MediaId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Medias_ArtistId",
                table: "Medias",
                column: "ArtistId",
                principalTable: "Medias",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Medias_MusicTrack_ArtistId",
                table: "Medias",
                column: "MusicTrack_ArtistId",
                principalTable: "Medias",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Medias_Medias_ArtistId",
                table: "Medias");

            migrationBuilder.DropForeignKey(
                name: "FK_Medias_Medias_MusicTrack_ArtistId",
                table: "Medias");

            migrationBuilder.DropTable(
                name: "MusicArtistCredits");

            migrationBuilder.DropIndex(
                name: "IX_Medias_ArtistId",
                table: "Medias");

            migrationBuilder.DropIndex(
                name: "IX_Medias_MusicTrack_ArtistId",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "PersonRoles");

            migrationBuilder.DropColumn(
                name: "ArtistId",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "ArtistType",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Biography",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "MusicTrack_ArtistId",
                table: "Medias");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "PersonRoles",
                newName: "IsGuest");
        }
    }
}
