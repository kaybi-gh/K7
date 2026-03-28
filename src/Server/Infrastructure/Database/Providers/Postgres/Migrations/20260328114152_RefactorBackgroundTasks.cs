using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RefactorBackgroundTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MaxRetryCount",
                table: "BackgroundTasks",
                newName: "MaxAttempts");

            migrationBuilder.RenameColumn(
                name: "RetryCount",
                table: "BackgroundTasks",
                newName: "AttemptCount");

            migrationBuilder.AddColumn<int>(
                name: "TimeoutSeconds",
                table: "BackgroundTasks",
                type: "integer",
                nullable: false,
                defaultValue: 300);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextRetryAfter",
                table: "BackgroundTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                table: "BackgroundTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundTasks_Status_Priority_Created",
                table: "BackgroundTasks",
                columns: new[] { "Status", "Priority", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundTasks_TargetEntityId",
                table: "BackgroundTasks",
                column: "TargetEntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BackgroundTasks_Status_Priority_Created",
                table: "BackgroundTasks");

            migrationBuilder.DropIndex(
                name: "IX_BackgroundTasks_TargetEntityId",
                table: "BackgroundTasks");

            migrationBuilder.DropColumn(
                name: "TimeoutSeconds",
                table: "BackgroundTasks");

            migrationBuilder.DropColumn(
                name: "NextRetryAfter",
                table: "BackgroundTasks");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "BackgroundTasks");

            migrationBuilder.RenameColumn(
                name: "AttemptCount",
                table: "BackgroundTasks",
                newName: "RetryCount");

            migrationBuilder.RenameColumn(
                name: "MaxAttempts",
                table: "BackgroundTasks",
                newName: "MaxRetryCount");
        }
    }
}
