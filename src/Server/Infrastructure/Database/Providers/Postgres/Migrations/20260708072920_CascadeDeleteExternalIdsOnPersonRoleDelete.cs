using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeleteExternalIdsOnPersonRoleDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalIds_PersonRoles_PersonRoleId",
                table: "ExternalIds");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalIds_PersonRoles_PersonRoleId",
                table: "ExternalIds",
                column: "PersonRoleId",
                principalTable: "PersonRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalIds_PersonRoles_PersonRoleId",
                table: "ExternalIds");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalIds_PersonRoles_PersonRoleId",
                table: "ExternalIds",
                column: "PersonRoleId",
                principalTable: "PersonRoles",
                principalColumn: "Id");
        }
    }
}
