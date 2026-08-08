using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations;

/// <inheritdoc />
public partial class AddSkipCount : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "SkipCount",
            table: "UserMediaStates",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "SkipCount",
            table: "SharedProfileMediaStates",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateIndex(
            name: "IX_UserMediaStates_UserId_SkipCount",
            table: "UserMediaStates",
            columns: new[] { "UserId", "SkipCount" });

        migrationBuilder.CreateIndex(
            name: "IX_SharedProfileMediaStates_SharedProfileId_SkipCount",
            table: "SharedProfileMediaStates",
            columns: new[] { "SharedProfileId", "SkipCount" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_UserMediaStates_UserId_SkipCount",
            table: "UserMediaStates");

        migrationBuilder.DropIndex(
            name: "IX_SharedProfileMediaStates_SharedProfileId_SkipCount",
            table: "SharedProfileMediaStates");

        migrationBuilder.DropColumn(
            name: "SkipCount",
            table: "UserMediaStates");

        migrationBuilder.DropColumn(
            name: "SkipCount",
            table: "SharedProfileMediaStates");
    }
}
