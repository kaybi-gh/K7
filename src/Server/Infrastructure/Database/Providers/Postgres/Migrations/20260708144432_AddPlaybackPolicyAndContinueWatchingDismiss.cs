using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybackPolicyAndContinueWatchingDismiss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExcludedFromContinueWatching",
                table: "UserMediaStates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "LastKnownDurationSeconds",
                table: "UserMediaStates",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExcludedFromContinueWatching",
                table: "UserMediaStates");

            migrationBuilder.DropColumn(
                name: "LastKnownDurationSeconds",
                table: "UserMediaStates");
        }
    }
}
