using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddExclusionFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdminExcluded",
                table: "UserMediaExclusions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSelfExcluded",
                table: "UserMediaExclusions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdminExcluded",
                table: "UserLibraryExclusions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSelfExcluded",
                table: "UserLibraryExclusions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LibraryId",
                table: "MetadataPictures",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Libraries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "Libraries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_LibraryId",
                table: "MetadataPictures",
                column: "LibraryId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataPictures_Libraries_LibraryId",
                table: "MetadataPictures",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MetadataPictures_Libraries_LibraryId",
                table: "MetadataPictures");

            migrationBuilder.DropIndex(
                name: "IX_MetadataPictures_LibraryId",
                table: "MetadataPictures");

            migrationBuilder.DropColumn(
                name: "IsAdminExcluded",
                table: "UserMediaExclusions");

            migrationBuilder.DropColumn(
                name: "IsSelfExcluded",
                table: "UserMediaExclusions");

            migrationBuilder.DropColumn(
                name: "IsAdminExcluded",
                table: "UserLibraryExclusions");

            migrationBuilder.DropColumn(
                name: "IsSelfExcluded",
                table: "UserLibraryExclusions");

            migrationBuilder.DropColumn(
                name: "LibraryId",
                table: "MetadataPictures");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Libraries");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "Libraries");
        }
    }
}
