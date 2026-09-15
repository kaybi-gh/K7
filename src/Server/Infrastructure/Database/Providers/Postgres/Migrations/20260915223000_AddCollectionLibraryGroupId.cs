using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations;

/// <inheritdoc />
[DbContext(typeof(K7.Server.Infrastructure.Database.Context.Data.ApplicationDbContext))]
[Migration("20260915223000_AddCollectionLibraryGroupId")]
public partial class AddCollectionLibraryGroupId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "LibraryGroupId",
            table: "Collections",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Collections_LibraryGroupId",
            table: "Collections",
            column: "LibraryGroupId");

        migrationBuilder.AddForeignKey(
            name: "FK_Collections_LibraryGroups_LibraryGroupId",
            table: "Collections",
            column: "LibraryGroupId",
            principalTable: "LibraryGroups",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Collections_LibraryGroups_LibraryGroupId",
            table: "Collections");

        migrationBuilder.DropIndex(
            name: "IX_Collections_LibraryGroupId",
            table: "Collections");

        migrationBuilder.DropColumn(
            name: "LibraryGroupId",
            table: "Collections");
    }
}
