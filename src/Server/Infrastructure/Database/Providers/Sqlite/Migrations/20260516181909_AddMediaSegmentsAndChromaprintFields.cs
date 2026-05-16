using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaSegmentsAndChromaprintFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ChromaprintAnalyzedAt",
                table: "IndexedFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChromaprintDurationSeconds",
                table: "IndexedFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ChromaprintFingerprint",
                table: "IndexedFiles",
                type: "BLOB",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MediaSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    StartMs = table.Column<long>(type: "INTEGER", nullable: false),
                    EndMs = table.Column<long>(type: "INTEGER", nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaSegments_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaSegments_MediaId",
                table: "MediaSegments",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaSegments_MediaId_Type",
                table: "MediaSegments",
                columns: new[] { "MediaId", "Type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaSegments");

            migrationBuilder.DropColumn(
                name: "ChromaprintAnalyzedAt",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "ChromaprintDurationSeconds",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "ChromaprintFingerprint",
                table: "IndexedFiles");
        }
    }
}
