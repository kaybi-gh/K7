using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeleteMetadataPicturesOnPersonRoleDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MetadataPictures_PersonRoles_PersonRoleId",
                table: "MetadataPictures");

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataPictures_PersonRoles_PersonRoleId",
                table: "MetadataPictures",
                column: "PersonRoleId",
                principalTable: "PersonRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MetadataPictures_PersonRoles_PersonRoleId",
                table: "MetadataPictures");

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataPictures_PersonRoles_PersonRoleId",
                table: "MetadataPictures",
                column: "PersonRoleId",
                principalTable: "PersonRoles",
                principalColumn: "Id");
        }
    }
}
