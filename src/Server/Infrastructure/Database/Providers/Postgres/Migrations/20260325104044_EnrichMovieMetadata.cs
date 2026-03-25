using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class EnrichMovieMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Budget",
                table: "Medias",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentRating",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Revenue",
                table: "Medias",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Budget",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "ContentRating",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Revenue",
                table: "Medias");
        }
    }
}
