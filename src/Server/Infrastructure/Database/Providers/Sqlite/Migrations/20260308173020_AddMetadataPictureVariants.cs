using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMetadataPictureVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MetadataPictures_FileMetadatas_VideoFileMetadataId",
                table: "MetadataPictures");

            migrationBuilder.AddColumn<double>(
                name: "ProgressPercentage",
                table: "UserMediaStates",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "MetadataPictureVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Size = table.Column<int>(type: "INTEGER", nullable: false),
                    LocalPath = table.Column<string>(type: "TEXT", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    MetadataPictureId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataPictureVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetadataPictureVariants_MetadataPictures_MetadataPictureId",
                        column: x => x.MetadataPictureId,
                        principalTable: "MetadataPictures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictureVariants_MetadataPictureId_Size",
                table: "MetadataPictureVariants",
                columns: new[] { "MetadataPictureId", "Size" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataPictures_FileMetadatas_VideoFileMetadataId",
                table: "MetadataPictures",
                column: "VideoFileMetadataId",
                principalTable: "FileMetadatas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MetadataPictures_FileMetadatas_VideoFileMetadataId",
                table: "MetadataPictures");

            migrationBuilder.DropTable(
                name: "MetadataPictureVariants");

            migrationBuilder.DropColumn(
                name: "ProgressPercentage",
                table: "UserMediaStates");

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataPictures_FileMetadatas_VideoFileMetadataId",
                table: "MetadataPictures",
                column: "VideoFileMetadataId",
                principalTable: "FileMetadatas",
                principalColumn: "Id");
        }
    }
}
