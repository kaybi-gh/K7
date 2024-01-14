using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaServer.Infrastructure.DatabaseProviders.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IndexedFiles_Medias_BaseMediaId",
                table: "IndexedFiles");

            migrationBuilder.DropIndex(
                name: "IX_IndexedFiles_BaseMediaId",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "BaseMediaId",
                table: "IndexedFiles");

            migrationBuilder.CreateTable(
                name: "BaseMediaIndexedFile",
                columns: table => new
                {
                    BaseMediaId = table.Column<int>(type: "integer", nullable: false),
                    IndexedFilesId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaseMediaIndexedFile", x => new { x.BaseMediaId, x.IndexedFilesId });
                    table.ForeignKey(
                        name: "FK_BaseMediaIndexedFile_IndexedFiles_IndexedFilesId",
                        column: x => x.IndexedFilesId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BaseMediaIndexedFile_Medias_BaseMediaId",
                        column: x => x.BaseMediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BaseMediaIndexedFile_IndexedFilesId",
                table: "BaseMediaIndexedFile",
                column: "IndexedFilesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BaseMediaIndexedFile");

            migrationBuilder.AddColumn<int>(
                name: "BaseMediaId",
                table: "IndexedFiles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_BaseMediaId",
                table: "IndexedFiles",
                column: "BaseMediaId");

            migrationBuilder.AddForeignKey(
                name: "FK_IndexedFiles_Medias_BaseMediaId",
                table: "IndexedFiles",
                column: "BaseMediaId",
                principalTable: "Medias",
                principalColumn: "Id");
        }
    }
}
