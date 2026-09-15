using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations;

/// <inheritdoc />
[DbContext(typeof(K7.Server.Infrastructure.Database.Context.Data.ApplicationDbContext))]
[Migration("20260915220000_AddDynamicCollections")]
public partial class AddDynamicCollections : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastEvaluatedAt",
            table: "Collections",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Limit",
            table: "Collections",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "OrderBy",
            table: "Collections",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "OrderDescending",
            table: "Collections",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<string>(
            name: "RuleFilter",
            table: "Collections",
            type: "jsonb",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastEvaluatedAt",
            table: "Collections");

        migrationBuilder.DropColumn(
            name: "Limit",
            table: "Collections");

        migrationBuilder.DropColumn(
            name: "OrderBy",
            table: "Collections");

        migrationBuilder.DropColumn(
            name: "OrderDescending",
            table: "Collections");

        migrationBuilder.DropColumn(
            name: "RuleFilter",
            table: "Collections");
    }
}
