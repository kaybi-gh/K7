using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddEphemeralStreamToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EphemeralStreamTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StreamSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EphemeralStreamTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EphemeralStreamTokens_StreamSessions_StreamSessionId",
                        column: x => x.StreamSessionId,
                        principalTable: "StreamSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EphemeralStreamTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EphemeralStreamTokens_ExpiresAt",
                table: "EphemeralStreamTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_EphemeralStreamTokens_StreamSessionId",
                table: "EphemeralStreamTokens",
                column: "StreamSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_EphemeralStreamTokens_Token",
                table: "EphemeralStreamTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EphemeralStreamTokens_UserId",
                table: "EphemeralStreamTokens",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EphemeralStreamTokens");
        }
    }
}
