using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class MigrateAlbumPosterToCover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MetadataPictureType: Poster = 1, Cover = 7
            // MediaType discriminator: MusicAlbum = 2
            migrationBuilder.Sql("""
                UPDATE MetadataPictures
                SET Type = 7
                WHERE Type = 1
                  AND MediaId IN (SELECT Id FROM Medias WHERE Type = 2)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE MetadataPictures
                SET Type = 1
                WHERE Type = 7
                  AND MediaId IN (SELECT Id FROM Medias WHERE Type = 2)
                """);
        }
    }
}
