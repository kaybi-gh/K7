using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddFoldedTextSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF Core cannot model a GIN index on a function. The query calls k7_fold_diacritics.
            migrationBuilder.Sql("""
                CREATE FUNCTION k7_fold_diacritics(value text)
                RETURNS text
                LANGUAGE sql
                IMMUTABLE
                PARALLEL SAFE
                STRICT
                AS $$
                  SELECT normalize(
                    regexp_replace(
                      normalize(value, NFD),
                      '[' || chr(768) || '-' || chr(879) || ']',
                      '',
                      'g'),
                    NFC)
                $$;

                CREATE INDEX "IX_Persons_Name_fold_trgm"
                    ON "Persons"
                    USING gin (k7_fold_diacritics("Name") gin_trgm_ops);

                CREATE INDEX "IX_PersonRoles_CharacterName_fold_trgm"
                    ON "PersonRoles"
                    USING gin (k7_fold_diacritics("CharacterName") gin_trgm_ops)
                    WHERE "Type" = 0;

                CREATE INDEX "IX_PersonRoles_VoiceActor_CharacterName_fold_trgm"
                    ON "PersonRoles"
                    USING gin (k7_fold_diacritics("VoiceActor_CharacterName") gin_trgm_ops)
                    WHERE "Type" = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX "IX_PersonRoles_VoiceActor_CharacterName_fold_trgm";
                DROP INDEX "IX_PersonRoles_CharacterName_fold_trgm";
                DROP INDEX "IX_Persons_Name_fold_trgm";
                DROP FUNCTION k7_fold_diacritics(text);
                """);
        }
    }
}
