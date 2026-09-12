using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations;

/// <inheritdoc />
public partial class AddAgeRestriction : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "AgeRestrictionEnabled",
            table: "Users",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateOnly>(
            name: "DateOfBirth",
            table: "Users",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "HideUnratedTitles",
            table: "Users",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "AgeRestrictionEnabled",
            table: "SharedProfiles",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "HideUnratedTitles",
            table: "SharedProfiles",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "ViewerDateOfBirth",
            table: "SharedProfiles",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AgeRestrictionEnabled",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "DateOfBirth",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "HideUnratedTitles",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "AgeRestrictionEnabled",
            table: "SharedProfiles");

        migrationBuilder.DropColumn(
            name: "HideUnratedTitles",
            table: "SharedProfiles");

        migrationBuilder.DropColumn(
            name: "ViewerDateOfBirth",
            table: "SharedProfiles");
    }
}
