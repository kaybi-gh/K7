using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations;

/// <inheritdoc />
[DbContext(typeof(K7.Server.Infrastructure.Database.Context.Data.ApplicationDbContext))]
[Migration("20260816100000_AddLibraryGroupExploreTapAction")]
public partial class AddLibraryGroupExploreTapAction : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ExploreTapAction",
            table: "LibraryGroups",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ExploreTapAction",
            table: "LibraryGroups");
    }
}
