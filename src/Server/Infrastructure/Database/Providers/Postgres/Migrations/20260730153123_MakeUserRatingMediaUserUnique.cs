using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class MakeUserRatingMediaUserUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Concurrent RateMedia / UpsertMediaReview could create duplicate LocalUser rows before
            // this unique index. Keep the rating referenced by a MediaReview when one exists (so
            // cascade delete does not wipe the review), otherwise the newest row.
            migrationBuilder.Sql("""
                DELETE FROM "Ratings" WHERE "Id" IN (
                    SELECT "Id" FROM (
                        SELECT r."Id",
                            ROW_NUMBER() OVER (
                                PARTITION BY r."MediaId", r."UserId"
                                ORDER BY
                                    CASE WHEN EXISTS (
                                        SELECT 1 FROM "MediaReviews" mr WHERE mr."UserRatingId" = r."Id"
                                    ) THEN 0 ELSE 1 END,
                                    r."LastModified" DESC,
                                    r."Id") AS rn
                        FROM "Ratings" r
                        WHERE r."Source" = 2 AND r."UserId" IS NOT NULL
                    ) duplicates
                    WHERE rn > 1
                );
                """);

            migrationBuilder.DropIndex(
                name: "IX_Ratings_MediaId_UserId",
                table: "Ratings");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_MediaId_UserId",
                table: "Ratings",
                columns: new[] { "MediaId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ratings_MediaId_UserId",
                table: "Ratings");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_MediaId_UserId",
                table: "Ratings",
                columns: new[] { "MediaId", "UserId" });
        }
    }
}
