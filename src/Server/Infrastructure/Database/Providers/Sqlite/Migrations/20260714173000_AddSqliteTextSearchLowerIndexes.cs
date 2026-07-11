using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSqliteTextSearchLowerIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_Medias_Title_lower"
                ON "Medias" (lower("Title"))
                WHERE "Title" IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_Persons_Name_lower"
                ON "Persons" (lower("Name"));
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_PersonRoles_CharacterName_lower"
                ON "PersonRoles" (lower("CharacterName"))
                WHERE "CharacterName" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_PersonRoles_CharacterName_lower\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Persons_Name_lower\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Medias_Title_lower\";");
        }
    }
}
