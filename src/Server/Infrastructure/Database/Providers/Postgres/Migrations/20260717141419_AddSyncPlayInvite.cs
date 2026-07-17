using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncPlayInvite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncPlayInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncPlayInvites", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncPlayInvites_CreatedAt",
                table: "SyncPlayInvites",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SyncPlayInvites_GroupId",
                table: "SyncPlayInvites",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncPlayInvites_Token",
                table: "SyncPlayInvites",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncPlayInvites");
        }
    }
}
