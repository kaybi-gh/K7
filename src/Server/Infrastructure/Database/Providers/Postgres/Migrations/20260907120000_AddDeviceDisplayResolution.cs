using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations;

[DbContext(typeof(K7.Server.Infrastructure.Database.Context.Data.ApplicationDbContext))]
[Migration("20260907120000_AddDeviceDisplayResolution")]
public partial class AddDeviceDisplayResolution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "DisplayHeight",
            table: "Devices",
            newName: "DisplayScreenHeight");

        migrationBuilder.RenameColumn(
            name: "DisplayWidth",
            table: "Devices",
            newName: "DisplayScreenWidth");

        migrationBuilder.AddColumn<double>(
            name: "DisplayResolutionHeight",
            table: "Devices",
            type: "double precision",
            nullable: false,
            defaultValue: 0.0);

        migrationBuilder.AddColumn<double>(
            name: "DisplayResolutionWidth",
            table: "Devices",
            type: "double precision",
            nullable: false,
            defaultValue: 0.0);

        migrationBuilder.Sql(
            """
            UPDATE "Devices"
            SET "DisplayResolutionHeight" = "DisplayScreenHeight",
                "DisplayResolutionWidth" = "DisplayScreenWidth";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DisplayResolutionHeight",
            table: "Devices");

        migrationBuilder.DropColumn(
            name: "DisplayResolutionWidth",
            table: "Devices");

        migrationBuilder.RenameColumn(
            name: "DisplayScreenHeight",
            table: "Devices",
            newName: "DisplayHeight");

        migrationBuilder.RenameColumn(
            name: "DisplayScreenWidth",
            table: "Devices",
            newName: "DisplayWidth");
    }
}
