using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Persons_Slug",
                table: "Persons");

            migrationBuilder.DropIndex(
                name: "IX_Medias_Slug",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Medias");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Persons",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Medias",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Persons_Slug",
                table: "Persons",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Medias_Slug",
                table: "Medias",
                column: "Slug",
                unique: true);
        }
    }
}
