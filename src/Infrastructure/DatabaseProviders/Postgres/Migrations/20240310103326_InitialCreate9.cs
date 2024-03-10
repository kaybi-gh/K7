using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaServer.Infrastructure.DatabaseProviders.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Medias_Libraries_LibraryId",
                table: "Medias");

            migrationBuilder.DropIndex(
                name: "IX_Medias_LibraryId",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Identification_ReleaseYear",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Identification_Title",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "LibraryId",
                table: "Medias");

            migrationBuilder.AddColumn<DateOnly>(
                name: "Identification_ReleaseYear",
                table: "IndexedFiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identification_Title",
                table: "IndexedFiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Identification_ReleaseYear",
                table: "IndexedFiles");

            migrationBuilder.DropColumn(
                name: "Identification_Title",
                table: "IndexedFiles");

            migrationBuilder.AddColumn<DateOnly>(
                name: "Identification_ReleaseYear",
                table: "Medias",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identification_Title",
                table: "Medias",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "LibraryId",
                table: "Medias",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Medias_LibraryId",
                table: "Medias",
                column: "LibraryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Medias_Libraries_LibraryId",
                table: "Medias",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
