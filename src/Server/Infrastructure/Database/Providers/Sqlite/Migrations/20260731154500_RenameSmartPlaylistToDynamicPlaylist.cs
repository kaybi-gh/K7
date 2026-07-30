using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RenameSmartPlaylistToDynamicPlaylist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "DynamicPlaylist" is 15 chars; previous max was 13 ("SmartPlaylist").
            migrationBuilder.AlterColumn<string>(
                name: "Discriminator",
                table: "Playlists",
                type: "TEXT",
                maxLength: 21,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 13);

            migrationBuilder.Sql("""
                UPDATE "Playlists"
                SET "Discriminator" = 'DynamicPlaylist'
                WHERE "Discriminator" = 'SmartPlaylist';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Playlists"
                SET "Discriminator" = 'SmartPlaylist'
                WHERE "Discriminator" = 'DynamicPlaylist';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Discriminator",
                table: "Playlists",
                type: "TEXT",
                maxLength: 13,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 21);
        }
    }
}
