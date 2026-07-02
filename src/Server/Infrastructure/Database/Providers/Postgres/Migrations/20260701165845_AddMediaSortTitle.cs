using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaSortTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SortTitle",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Medias_SortTitle",
                table: "Medias",
                column: "SortTitle");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Medias_SortTitle",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "SortTitle",
                table: "Medias");
        }
    }
}
