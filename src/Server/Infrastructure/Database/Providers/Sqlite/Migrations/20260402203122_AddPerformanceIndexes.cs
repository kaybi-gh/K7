using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserMediaStates_UserId",
                table: "UserMediaStates");

            migrationBuilder.DropIndex(
                name: "IX_UserMediaExclusions_UserId",
                table: "UserMediaExclusions");

            migrationBuilder.DropIndex(
                name: "IX_UserLibraryExclusions_UserId",
                table: "UserLibraryExclusions");

            migrationBuilder.CreateIndex(
                name: "IX_UserMediaStates_UserId_IsCompleted_LastInteractedAt",
                table: "UserMediaStates",
                columns: new[] { "UserId", "IsCompleted", "LastInteractedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMediaStates_UserId_LastInteractedAt",
                table: "UserMediaStates",
                columns: new[] { "UserId", "LastInteractedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMediaStates_UserId_MediaId",
                table: "UserMediaStates",
                columns: new[] { "UserId", "MediaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMediaStates_UserId_PlayCount",
                table: "UserMediaStates",
                columns: new[] { "UserId", "PlayCount" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMediaExclusions_UserId_MediaId",
                table: "UserMediaExclusions",
                columns: new[] { "UserId", "MediaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryExclusions_UserId_LibraryId",
                table: "UserLibraryExclusions",
                columns: new[] { "UserId", "LibraryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_MediaId_UserId",
                table: "Ratings",
                columns: new[] { "MediaId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Medias_OriginalTitle",
                table: "Medias",
                column: "OriginalTitle");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_ReleaseDate",
                table: "Medias",
                column: "ReleaseDate");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_Title",
                table: "Medias",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_Type",
                table: "Medias",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserMediaStates_UserId_IsCompleted_LastInteractedAt",
                table: "UserMediaStates");

            migrationBuilder.DropIndex(
                name: "IX_UserMediaStates_UserId_LastInteractedAt",
                table: "UserMediaStates");

            migrationBuilder.DropIndex(
                name: "IX_UserMediaStates_UserId_MediaId",
                table: "UserMediaStates");

            migrationBuilder.DropIndex(
                name: "IX_UserMediaStates_UserId_PlayCount",
                table: "UserMediaStates");

            migrationBuilder.DropIndex(
                name: "IX_UserMediaExclusions_UserId_MediaId",
                table: "UserMediaExclusions");

            migrationBuilder.DropIndex(
                name: "IX_UserLibraryExclusions_UserId_LibraryId",
                table: "UserLibraryExclusions");

            migrationBuilder.DropIndex(
                name: "IX_Ratings_MediaId_UserId",
                table: "Ratings");

            migrationBuilder.DropIndex(
                name: "IX_Medias_OriginalTitle",
                table: "Medias");

            migrationBuilder.DropIndex(
                name: "IX_Medias_ReleaseDate",
                table: "Medias");

            migrationBuilder.DropIndex(
                name: "IX_Medias_Title",
                table: "Medias");

            migrationBuilder.DropIndex(
                name: "IX_Medias_Type",
                table: "Medias");

            migrationBuilder.CreateIndex(
                name: "IX_UserMediaStates_UserId",
                table: "UserMediaStates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMediaExclusions_UserId",
                table: "UserMediaExclusions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryExclusions_UserId",
                table: "UserLibraryExclusions",
                column: "UserId");
        }
    }
}
