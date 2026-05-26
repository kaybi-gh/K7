using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "MetadataPictures",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_UserId",
                table: "MetadataPictures",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataPictures_Users_UserId",
                table: "MetadataPictures",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MetadataPictures_Users_UserId",
                table: "MetadataPictures");

            migrationBuilder.DropIndex(
                name: "IX_MetadataPictures_UserId",
                table: "MetadataPictures");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "MetadataPictures");
        }
    }
}
