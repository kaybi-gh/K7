using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexedFileLibraryCreatedIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RemoteIndexedFiles_LibraryId",
                table: "RemoteIndexedFiles");

            migrationBuilder.DropIndex(
                name: "IX_IndexedFiles_LibraryId",
                table: "IndexedFiles");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteIndexedFiles_LibraryId_Created",
                table: "RemoteIndexedFiles",
                columns: new[] { "LibraryId", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_LibraryId_Created",
                table: "IndexedFiles",
                columns: new[] { "LibraryId", "Created" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RemoteIndexedFiles_LibraryId_Created",
                table: "RemoteIndexedFiles");

            migrationBuilder.DropIndex(
                name: "IX_IndexedFiles_LibraryId_Created",
                table: "IndexedFiles");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteIndexedFiles_LibraryId",
                table: "RemoteIndexedFiles",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_LibraryId",
                table: "IndexedFiles",
                column: "LibraryId");
        }
    }
}
