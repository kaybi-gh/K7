using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations;

/// <inheritdoc />
public partial class AddViewingGroups : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ViewingGroupId",
            table: "MediaPlaybackSessions",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ViewingGroupNameSnapshot",
            table: "MediaPlaybackSessions",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "MediaPlaybackSessionCoViewers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MediaPlaybackSessionCoViewers", x => x.Id);
                table.ForeignKey(
                    name: "FK_MediaPlaybackSessionCoViewers_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ViewingGroups",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                HostUserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                PinHash = table.Column<string>(type: "text", nullable: true),
                Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "text", nullable: true),
                LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastModifiedBy = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ViewingGroups", x => x.Id);
                table.ForeignKey(
                    name: "FK_ViewingGroups_Users_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ViewingGroups_Users_HostUserId",
                    column: x => x.HostUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ViewingGroupMembers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ViewingGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ViewingGroupMembers", x => x.Id);
                table.ForeignKey(
                    name: "FK_ViewingGroupMembers_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ViewingGroupMembers_ViewingGroups_ViewingGroupId",
                    column: x => x.ViewingGroupId,
                    principalTable: "ViewingGroups",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MediaPlaybackSessions_ViewingGroupId",
            table: "MediaPlaybackSessions",
            column: "ViewingGroupId");

        migrationBuilder.CreateIndex(
            name: "IX_MediaPlaybackSessionCoViewers_ReferenceId_UserId",
            table: "MediaPlaybackSessionCoViewers",
            columns: new[] { "ReferenceId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MediaPlaybackSessionCoViewers_UserId",
            table: "MediaPlaybackSessionCoViewers",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_ViewingGroupMembers_UserId",
            table: "ViewingGroupMembers",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_ViewingGroupMembers_ViewingGroupId_UserId",
            table: "ViewingGroupMembers",
            columns: new[] { "ViewingGroupId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ViewingGroups_CreatedByUserId",
            table: "ViewingGroups",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ViewingGroups_HostUserId",
            table: "ViewingGroups",
            column: "HostUserId");

        migrationBuilder.AddForeignKey(
            name: "FK_MediaPlaybackSessions_ViewingGroups_ViewingGroupId",
            table: "MediaPlaybackSessions",
            column: "ViewingGroupId",
            principalTable: "ViewingGroups",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_MediaPlaybackSessions_ViewingGroups_ViewingGroupId",
            table: "MediaPlaybackSessions");

        migrationBuilder.DropTable(
            name: "MediaPlaybackSessionCoViewers");

        migrationBuilder.DropTable(
            name: "ViewingGroupMembers");

        migrationBuilder.DropTable(
            name: "ViewingGroups");

        migrationBuilder.DropIndex(
            name: "IX_MediaPlaybackSessions_ViewingGroupId",
            table: "MediaPlaybackSessions");

        migrationBuilder.DropColumn(
            name: "ViewingGroupId",
            table: "MediaPlaybackSessions");

        migrationBuilder.DropColumn(
            name: "ViewingGroupNameSnapshot",
            table: "MediaPlaybackSessions");
    }
}
