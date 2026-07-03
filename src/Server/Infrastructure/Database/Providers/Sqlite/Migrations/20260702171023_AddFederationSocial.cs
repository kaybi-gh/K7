using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddFederationSocial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_PeerServerId",
                table: "Users");

            migrationBuilder.AddColumn<Guid>(
                name: "OriginUserId",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VisibilityScope",
                table: "Playlists",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FederationAssertionSecret",
                table: "PeerServers",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VisibilityScope",
                table: "Collections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MediaReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserRatingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Emoji = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Created = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<string>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaReviews_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaReviews_Ratings_UserRatingId",
                        column: x => x.UserRatingId,
                        principalTable: "Ratings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaReviews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PeerSocialAgreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PeerServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AllowOutbound = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowInbound = table.Column<bool>(type: "INTEGER", nullable: false),
                    Created = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<string>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeerSocialAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeerSocialAgreements_PeerServers_PeerServerId",
                        column: x => x.PeerServerId,
                        principalTable: "PeerServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VisibilityGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentType = table.Column<int>(type: "INTEGER", nullable: true),
                    PlaylistId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CollectionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TargetUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TargetPeerServerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TargetOriginUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisibilityGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisibilityGrants_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_PeerServerId_OriginUserId",
                table: "Users",
                columns: new[] { "PeerServerId", "OriginUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaReviews_MediaId_UserId",
                table: "MediaReviews",
                columns: new[] { "MediaId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaReviews_UserId",
                table: "MediaReviews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaReviews_UserRatingId",
                table: "MediaReviews",
                column: "UserRatingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeerSocialAgreements_PeerServerId_ContentType",
                table: "PeerSocialAgreements",
                columns: new[] { "PeerServerId", "ContentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisibilityGrants_OwnerUserId",
                table: "VisibilityGrants",
                column: "OwnerUserId");

            migrationBuilder.Sql("UPDATE \"Collections\" SET \"VisibilityScope\" = 1 WHERE \"IsPublic\" = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaReviews");

            migrationBuilder.DropTable(
                name: "PeerSocialAgreements");

            migrationBuilder.DropTable(
                name: "VisibilityGrants");

            migrationBuilder.DropIndex(
                name: "IX_Users_PeerServerId_OriginUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OriginUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VisibilityScope",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "FederationAssertionSecret",
                table: "PeerServers");

            migrationBuilder.DropColumn(
                name: "VisibilityScope",
                table: "Collections");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PeerServerId",
                table: "Users",
                column: "PeerServerId");
        }
    }
}
