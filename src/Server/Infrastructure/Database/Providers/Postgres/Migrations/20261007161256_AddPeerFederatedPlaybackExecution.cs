using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddPeerFederatedPlaybackExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FederatedPlaybackExecution",
                table: "StreamSessions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FederatedPlaybackExecution",
                table: "PeerServers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Peer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FederatedPlaybackExecution",
                table: "StreamSessions");

            migrationBuilder.DropColumn(
                name: "FederatedPlaybackExecution",
                table: "PeerServers");
        }
    }
}
