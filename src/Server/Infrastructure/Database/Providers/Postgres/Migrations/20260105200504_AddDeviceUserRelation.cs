using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceUserRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SupportsHDR",
                table: "Devices",
                newName: "PlaybackCapabilities_SupportsHDR");

            migrationBuilder.RenameColumn(
                name: "SupportedSubtitlesCodecs",
                table: "Devices",
                newName: "PlaybackCapabilities_SupportedSubtitlesCodecs");

            migrationBuilder.RenameColumn(
                name: "SupportedMediaFormatIds",
                table: "Devices",
                newName: "PlaybackCapabilities_SupportedMediaFormatIds");

            migrationBuilder.CreateTable(
                name: "DeviceUser",
                columns: table => new
                {
                    DevicesId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsersId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceUser", x => new { x.DevicesId, x.UsersId });
                    table.ForeignKey(
                        name: "FK_DeviceUser_Devices_DevicesId",
                        column: x => x.DevicesId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviceUser_User_UsersId",
                        column: x => x.UsersId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceUser_UsersId",
                table: "DeviceUser",
                column: "UsersId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceUser");

            migrationBuilder.RenameColumn(
                name: "PlaybackCapabilities_SupportsHDR",
                table: "Devices",
                newName: "SupportsHDR");

            migrationBuilder.RenameColumn(
                name: "PlaybackCapabilities_SupportedSubtitlesCodecs",
                table: "Devices",
                newName: "SupportedSubtitlesCodecs");

            migrationBuilder.RenameColumn(
                name: "PlaybackCapabilities_SupportedMediaFormatIds",
                table: "Devices",
                newName: "SupportedMediaFormatIds");
        }
    }
}
