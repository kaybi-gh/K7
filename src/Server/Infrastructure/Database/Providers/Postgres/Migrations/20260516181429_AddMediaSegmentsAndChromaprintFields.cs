using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
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
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChromaprintDurationSeconds",
                table: "IndexedFiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ChromaprintFingerprint",
                table: "IndexedFiles",
                type: "bytea",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MediaSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    StartMs = table.Column<long>(type: "bigint", nullable: false),
                    EndMs = table.Column<long>(type: "bigint", nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
