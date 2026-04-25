using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddExclusionFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdminExcluded",
                table: "UserMediaExclusions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSelfExcluded",
                table: "UserMediaExclusions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdminExcluded",
                table: "UserLibraryExclusions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSelfExcluded",
                table: "UserLibraryExclusions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdminExcluded",
                table: "UserMediaExclusions");

            migrationBuilder.DropColumn(
                name: "IsSelfExcluded",
                table: "UserMediaExclusions");

            migrationBuilder.DropColumn(
                name: "IsAdminExcluded",
                table: "UserLibraryExclusions");

            migrationBuilder.DropColumn(
                name: "IsSelfExcluded",
                table: "UserLibraryExclusions");
        }
    }
}
