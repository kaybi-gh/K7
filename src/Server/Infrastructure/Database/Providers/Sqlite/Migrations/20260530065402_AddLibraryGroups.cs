using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MetadataPictures_Libraries_LibraryId",
                table: "MetadataPictures");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Libraries");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "Libraries");

            migrationBuilder.RenameColumn(
                name: "LibraryId",
                table: "MetadataPictures",
                newName: "LibraryGroupId");

            migrationBuilder.RenameIndex(
                name: "IX_MetadataPictures_LibraryId",
                table: "MetadataPictures",
                newName: "IX_MetadataPictures_LibraryGroupId");

            migrationBuilder.AddColumn<Guid>(
                name: "LibraryGroupId",
                table: "Libraries",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "LibraryGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MediaType = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Created = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<string>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryGroups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Libraries_LibraryGroupId",
                table: "Libraries",
                column: "LibraryGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Libraries_LibraryGroups_LibraryGroupId",
                table: "Libraries",
                column: "LibraryGroupId",
                principalTable: "LibraryGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataPictures_LibraryGroups_LibraryGroupId",
                table: "MetadataPictures",
                column: "LibraryGroupId",
                principalTable: "LibraryGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Libraries_LibraryGroups_LibraryGroupId",
                table: "Libraries");

            migrationBuilder.DropForeignKey(
                name: "FK_MetadataPictures_LibraryGroups_LibraryGroupId",
                table: "MetadataPictures");

            migrationBuilder.DropTable(
                name: "LibraryGroups");

            migrationBuilder.DropIndex(
                name: "IX_Libraries_LibraryGroupId",
                table: "Libraries");

            migrationBuilder.DropColumn(
                name: "LibraryGroupId",
                table: "Libraries");

            migrationBuilder.RenameColumn(
                name: "LibraryGroupId",
                table: "MetadataPictures",
                newName: "LibraryId");

            migrationBuilder.RenameIndex(
                name: "IX_MetadataPictures_LibraryGroupId",
                table: "MetadataPictures",
                newName: "IX_MetadataPictures_LibraryId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataPictures_Libraries_LibraryId",
                table: "MetadataPictures",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
