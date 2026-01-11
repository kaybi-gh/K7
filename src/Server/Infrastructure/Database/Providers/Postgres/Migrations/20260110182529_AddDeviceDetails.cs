using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientType",
                table: "Devices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NativeDeviceDetails_RawDeviceType",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NativeDeviceDetails_RawIdiom",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NativeDeviceDetails_RawManufacturer",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NativeDeviceDetails_RawModel",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NativeDeviceDetails_RawName",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NativeDeviceDetails_RawPlatform",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NativeDeviceDetails_RawVersion",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WebDeviceDetails_Browser",
                table: "Devices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebDeviceDetails_RawBrowserName",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebDeviceDetails_RawBrowserVersion",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebDeviceDetails_RawEngineName",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebDeviceDetails_RawEngineVersion",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebDeviceDetails_RawOperatingSystemName",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebDeviceDetails_RawOperatingSystemVersion",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebDeviceDetails_RawOperatingSystemVersionName",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebDeviceDetails_RawPlatformType",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebDeviceDetails_RawUserAgent",
                table: "Devices",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientType",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "NativeDeviceDetails_RawDeviceType",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "NativeDeviceDetails_RawIdiom",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "NativeDeviceDetails_RawManufacturer",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "NativeDeviceDetails_RawModel",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "NativeDeviceDetails_RawName",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "NativeDeviceDetails_RawPlatform",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "NativeDeviceDetails_RawVersion",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "WebDeviceDetails_Browser",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "WebDeviceDetails_RawBrowserName",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "WebDeviceDetails_RawBrowserVersion",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "WebDeviceDetails_RawEngineName",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "WebDeviceDetails_RawEngineVersion",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "WebDeviceDetails_RawOperatingSystemName",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "WebDeviceDetails_RawOperatingSystemVersion",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "WebDeviceDetails_RawOperatingSystemVersionName",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "WebDeviceDetails_RawPlatformType",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "WebDeviceDetails_RawUserAgent",
                table: "Devices");
        }
    }
}
