using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddPeerAutoAddNewLibraries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoAddNewLibraries",
                table: "PeerServers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoAddNewLibraries",
                table: "PeerServers");
        }
    }
}
