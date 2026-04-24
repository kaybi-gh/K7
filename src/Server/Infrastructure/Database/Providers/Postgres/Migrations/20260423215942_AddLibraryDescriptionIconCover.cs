using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryDescriptionIconCover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LibraryId",
                table: "MetadataPictures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Libraries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "Libraries",
                type: "text",
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
