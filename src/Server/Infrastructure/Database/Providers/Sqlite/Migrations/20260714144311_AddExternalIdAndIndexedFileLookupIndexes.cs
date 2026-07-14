using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalIdAndIndexedFileLookupIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ExternalIds_ProviderName_Value",
                table: "ExternalIds",
                columns: new[] { "ProviderName", "Value" });

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_Hash",
                table: "IndexedFiles",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_Path",
                table: "IndexedFiles",
                column: "Path",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IndexedFiles_Path",
                table: "IndexedFiles");

            migrationBuilder.DropIndex(
                name: "IX_IndexedFiles_Hash",
                table: "IndexedFiles");

            migrationBuilder.DropIndex(
                name: "IX_ExternalIds_ProviderName_Value",
                table: "ExternalIds");
        }
    }
}
