using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddTextSearchTrigramIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_Medias_Title_trgm"
                ON "Medias" USING gin (lower("Title") gin_trgm_ops)
                WHERE "Title" IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_Persons_Name_trgm"
                ON "Persons" USING gin (lower("Name") gin_trgm_ops);
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX "IX_PersonRoles_CharacterName_trgm"
                ON "PersonRoles" USING gin (lower("CharacterName") gin_trgm_ops)
                WHERE "CharacterName" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_PersonRoles_CharacterName_trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Persons_Name_trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Medias_Title_trgm\";");
        }
    }
}
