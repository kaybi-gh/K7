using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations;

/// <inheritdoc />
public partial class AddSharedProfileAvatar : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "SharedProfileId",
            table: "MetadataPictures",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_MetadataPictures_SharedProfileId",
            table: "MetadataPictures",
            column: "SharedProfileId");

        migrationBuilder.AddForeignKey(
            name: "FK_MetadataPictures_SharedProfiles_SharedProfileId",
            table: "MetadataPictures",
            column: "SharedProfileId",
            principalTable: "SharedProfiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_MetadataPictures_SharedProfiles_SharedProfileId",
            table: "MetadataPictures");

        migrationBuilder.DropIndex(
            name: "IX_MetadataPictures_SharedProfileId",
            table: "MetadataPictures");

        migrationBuilder.DropColumn(
            name: "SharedProfileId",
            table: "MetadataPictures");
    }
}
