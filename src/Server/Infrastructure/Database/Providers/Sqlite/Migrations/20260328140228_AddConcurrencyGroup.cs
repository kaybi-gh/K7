using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConcurrencyGroup",
                table: "BackgroundTasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundTasks_ConcurrencyGroup",
                table: "BackgroundTasks",
                column: "ConcurrencyGroup");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BackgroundTasks_ConcurrencyGroup",
                table: "BackgroundTasks");

            migrationBuilder.DropColumn(
                name: "ConcurrencyGroup",
                table: "BackgroundTasks");
        }
    }
}
