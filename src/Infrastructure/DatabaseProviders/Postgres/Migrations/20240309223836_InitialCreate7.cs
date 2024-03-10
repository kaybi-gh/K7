using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaServer.Infrastructure.DatabaseProviders.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Medias_Metadatas_MetadataId",
                table: "Medias");

            migrationBuilder.DropIndex(
                name: "IX_Medias_MetadataId",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "MetadataId",
                table: "Medias");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MetadataId",
                table: "Medias",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Medias_MetadataId",
                table: "Medias",
                column: "MetadataId");

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Metadatas_MetadataId",
                table: "Medias",
                column: "MetadataId",
                principalTable: "Metadatas",
                principalColumn: "Id");
        }
    }
}
