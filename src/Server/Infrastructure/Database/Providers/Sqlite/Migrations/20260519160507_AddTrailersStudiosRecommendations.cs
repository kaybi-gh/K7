using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddTrailersStudiosRecommendations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Serie_Studios",
                table: "Medias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Studios",
                table: "Medias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Trailers",
                table: "Medias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MediaRecommendations",
                columns: table => new
                {
                    MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RecommendedIds = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaRecommendations", x => new { x.MediaId, x.ProviderName });
                    table.ForeignKey(
                        name: "FK_MediaRecommendations_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaRecommendations");

            migrationBuilder.DropColumn(
                name: "Serie_Studios",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Studios",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Trailers",
                table: "Medias");
        }
    }
}
