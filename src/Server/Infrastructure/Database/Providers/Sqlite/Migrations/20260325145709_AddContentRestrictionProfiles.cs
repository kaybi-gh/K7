using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddContentRestrictionProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContentRestrictionProfileId",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContentRestrictionProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    MatchCondition = table.Column<int>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    Rules = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentRestrictionProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ContentRestrictionProfileId",
                table: "Users",
                column: "ContentRestrictionProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_ContentRestrictionProfiles_ContentRestrictionProfileId",
                table: "Users",
                column: "ContentRestrictionProfileId",
                principalTable: "ContentRestrictionProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_ContentRestrictionProfiles_ContentRestrictionProfileId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "ContentRestrictionProfiles");

            migrationBuilder.DropIndex(
                name: "IX_Users_ContentRestrictionProfileId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ContentRestrictionProfileId",
                table: "Users");
        }
    }
}
