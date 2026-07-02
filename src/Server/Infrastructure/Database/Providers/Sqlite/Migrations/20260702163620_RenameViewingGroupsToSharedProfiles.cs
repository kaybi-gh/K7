using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RenameViewingGroupsToSharedProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaPlaybackSessions_ViewingGroups_ViewingGroupId",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_ViewingGroupMembers_ViewingGroups_ViewingGroupId",
                table: "ViewingGroupMembers");

            migrationBuilder.RenameTable(
                name: "ViewingGroups",
                newName: "SharedProfiles");

            migrationBuilder.RenameTable(
                name: "ViewingGroupMembers",
                newName: "SharedProfileMembers");

            migrationBuilder.RenameColumn(
                name: "ViewingGroupId",
                table: "MediaPlaybackSessions",
                newName: "SharedProfileId");

            migrationBuilder.RenameColumn(
                name: "ViewingGroupNameSnapshot",
                table: "MediaPlaybackSessions",
                newName: "SharedProfileNameSnapshot");

            migrationBuilder.RenameColumn(
                name: "ViewingGroupId",
                table: "SharedProfileMembers",
                newName: "SharedProfileId");

            migrationBuilder.RenameIndex(
                name: "IX_MediaPlaybackSessions_ViewingGroupId",
                table: "MediaPlaybackSessions",
                newName: "IX_MediaPlaybackSessions_SharedProfileId");

            migrationBuilder.RenameIndex(
                name: "IX_ViewingGroupMembers_UserId",
                table: "SharedProfileMembers",
                newName: "IX_SharedProfileMembers_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_ViewingGroupMembers_ViewingGroupId_UserId",
                table: "SharedProfileMembers",
                newName: "IX_SharedProfileMembers_SharedProfileId_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_ViewingGroups_CreatedByUserId",
                table: "SharedProfiles",
                newName: "IX_SharedProfiles_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_ViewingGroups_HostUserId",
                table: "SharedProfiles",
                newName: "IX_SharedProfiles_HostUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaPlaybackSessions_SharedProfiles_SharedProfileId",
                table: "MediaPlaybackSessions",
                column: "SharedProfileId",
                principalTable: "SharedProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SharedProfileMembers_SharedProfiles_SharedProfileId",
                table: "SharedProfileMembers",
                column: "SharedProfileId",
                principalTable: "SharedProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaPlaybackSessions_SharedProfiles_SharedProfileId",
                table: "MediaPlaybackSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_SharedProfileMembers_SharedProfiles_SharedProfileId",
                table: "SharedProfileMembers");

            migrationBuilder.RenameTable(
                name: "SharedProfiles",
                newName: "ViewingGroups");

            migrationBuilder.RenameTable(
                name: "SharedProfileMembers",
                newName: "ViewingGroupMembers");

            migrationBuilder.RenameColumn(
                name: "SharedProfileId",
                table: "MediaPlaybackSessions",
                newName: "ViewingGroupId");

            migrationBuilder.RenameColumn(
                name: "SharedProfileNameSnapshot",
                table: "MediaPlaybackSessions",
                newName: "ViewingGroupNameSnapshot");

            migrationBuilder.RenameColumn(
                name: "SharedProfileId",
                table: "ViewingGroupMembers",
                newName: "ViewingGroupId");

            migrationBuilder.RenameIndex(
                name: "IX_MediaPlaybackSessions_SharedProfileId",
                table: "MediaPlaybackSessions",
                newName: "IX_MediaPlaybackSessions_ViewingGroupId");

            migrationBuilder.RenameIndex(
                name: "IX_SharedProfileMembers_UserId",
                table: "ViewingGroupMembers",
                newName: "IX_ViewingGroupMembers_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_SharedProfileMembers_SharedProfileId_UserId",
                table: "ViewingGroupMembers",
                newName: "IX_ViewingGroupMembers_ViewingGroupId_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_SharedProfiles_CreatedByUserId",
                table: "ViewingGroups",
                newName: "IX_ViewingGroups_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_SharedProfiles_HostUserId",
                table: "ViewingGroups",
                newName: "IX_ViewingGroups_HostUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaPlaybackSessions_ViewingGroups_ViewingGroupId",
                table: "MediaPlaybackSessions",
                column: "ViewingGroupId",
                principalTable: "ViewingGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ViewingGroupMembers_ViewingGroups_ViewingGroupId",
                table: "ViewingGroupMembers",
                column: "ViewingGroupId",
                principalTable: "ViewingGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
