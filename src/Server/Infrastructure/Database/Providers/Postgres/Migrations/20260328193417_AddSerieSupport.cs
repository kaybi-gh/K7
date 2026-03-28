using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddSerieSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Medias_Medias_SerieId",
                table: "Medias");

            migrationBuilder.DropIndex(
                name: "IX_Medias_SeasonId",
                table: "Medias");

            migrationBuilder.DropIndex(
                name: "IX_Medias_SerieSeason_SerieId",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "IsComposite",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "IsSplitPart",
                table: "IndexedFiles");

            migrationBuilder.AddColumn<int>(
                name: "AbsoluteNumber",
                table: "Medias",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AirDate",
                table: "Medias",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EpisodeNumber",
                table: "Medias",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Network",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Runtime",
                table: "Medias",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeasonNumber",
                table: "Medias",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerieEpisode_Overview",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerieSeason_Overview",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Serie_ContentRating",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Serie_OriginalLanguage",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Serie_Overview",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Identification_AbsoluteNumber",
                table: "IndexedFiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Identification_EpisodeNumber",
                table: "IndexedFiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Identification_SeasonNumber",
                table: "IndexedFiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identification_SeriesTitle",
                table: "IndexedFiles",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Medias_SeasonId_EpisodeNumber",
                table: "Medias",
                columns: new[] { "SeasonId", "EpisodeNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Medias_SerieSeason_SerieId_SeasonNumber",
                table: "Medias",
                columns: new[] { "SerieSeason_SerieId", "SeasonNumber" });

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Medias_SerieId",
                table: "Medias",
                column: "SerieId",
                principalTable: "Medias",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Medias_Medias_SerieId",
                table: "Medias");

            migrationBuilder.DropIndex(
                name: "IX_Medias_SeasonId_EpisodeNumber",
                table: "Medias");

            migrationBuilder.DropIndex(
                name: "IX_Medias_SerieSeason_SerieId_SeasonNumber",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "AbsoluteNumber",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "AirDate",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "EpisodeNumber",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Network",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Runtime",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "SeasonNumber",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "SerieEpisode_Overview",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "SerieSeason_Overview",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Serie_ContentRating",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Serie_OriginalLanguage",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Serie_Overview",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Identification_AbsoluteNumber",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "Identification_EpisodeNumber",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "Identification_SeasonNumber",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "Identification_SeriesTitle",
                table: "IndexedFiles");

            migrationBuilder.AddColumn<bool>(
                name: "IsComposite",
                table: "IndexedFiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSplitPart",
                table: "IndexedFiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Medias_SeasonId",
                table: "Medias",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_SerieSeason_SerieId",
                table: "Medias",
                column: "SerieSeason_SerieId");

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Medias_SerieId",
                table: "Medias",
                column: "SerieId",
                principalTable: "Medias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
