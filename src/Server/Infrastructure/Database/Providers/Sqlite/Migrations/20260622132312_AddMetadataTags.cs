using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMetadataTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetadataTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    NormalizedKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaMetadataTags",
                columns: table => new
                {
                    MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MetadataTagId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaMetadataTags", x => new { x.MediaId, x.MetadataTagId });
                    table.ForeignKey(
                        name: "FK_MediaMetadataTags_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaMetadataTags_MetadataTags_MetadataTagId",
                        column: x => x.MetadataTagId,
                        principalTable: "MetadataTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaMetadataTags_MetadataTagId",
                table: "MediaMetadataTags",
                column: "MetadataTagId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataTags_Kind_NormalizedKey",
                table: "MetadataTags",
                columns: new[] { "Kind", "NormalizedKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaMetadataTags");

            migrationBuilder.DropTable(
                name: "MetadataTags");
        }
    }
}
