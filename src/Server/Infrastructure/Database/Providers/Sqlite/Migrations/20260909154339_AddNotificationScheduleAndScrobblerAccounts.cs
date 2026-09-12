using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationScheduleAndScrobblerAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CooldownSeconds",
                table: "NotificationRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSentAt",
                table: "NotificationRules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScheduleWindows",
                table: "NotificationRules",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "UserScrobblerAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    MediaTypes = table.Column<string>(type: "text", nullable: false),
                    IncludeNowPlaying = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Created = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<string>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserScrobblerAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserScrobblerAccounts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserScrobblerAccounts_UserId",
                table: "UserScrobblerAccounts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserScrobblerAccounts_UserId_Provider",
                table: "UserScrobblerAccounts",
                columns: new[] { "UserId", "Provider" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserScrobblerAccounts");

            migrationBuilder.DropColumn(
                name: "CooldownSeconds",
                table: "NotificationRules");

            migrationBuilder.DropColumn(
                name: "LastSentAt",
                table: "NotificationRules");

            migrationBuilder.DropColumn(
                name: "ScheduleWindows",
                table: "NotificationRules");
        }
    }
}
