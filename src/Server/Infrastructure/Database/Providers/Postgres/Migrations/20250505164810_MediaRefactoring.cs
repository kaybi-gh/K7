using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class MediaRefactoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalIds_MediaMetadatas_MetadataId",
                table: "ExternalIds");

            migrationBuilder.DropForeignKey(
                name: "FK_MetadataPictures_MediaMetadatas_MetadataId",
                table: "MetadataPictures");

            migrationBuilder.DropForeignKey(
                name: "FK_PersonRoles_MediaMetadatas_MetadataId",
                table: "PersonRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_Ratings_MediaMetadatas_MetadataId",
                table: "Ratings");

            migrationBuilder.DropTable(
                name: "MediaMetadatas");

            migrationBuilder.RenameColumn(
                name: "MetadataId",
                table: "Ratings",
                newName: "MediaId");

            migrationBuilder.RenameIndex(
                name: "IX_Ratings_MetadataId",
                table: "Ratings",
                newName: "IX_Ratings_MediaId");

            migrationBuilder.RenameColumn(
                name: "MetadataId",
                table: "PersonRoles",
                newName: "MediaId");

            migrationBuilder.RenameIndex(
                name: "IX_PersonRoles_MetadataId",
                table: "PersonRoles",
                newName: "IX_PersonRoles_MediaId");

            migrationBuilder.RenameColumn(
                name: "MetadataId",
                table: "MetadataPictures",
                newName: "MediaId");

            migrationBuilder.RenameIndex(
                name: "IX_MetadataPictures_MetadataId",
                table: "MetadataPictures",
                newName: "IX_MetadataPictures_MediaId");

            migrationBuilder.RenameColumn(
                name: "MetadataId",
                table: "ExternalIds",
                newName: "MediaId");

            migrationBuilder.RenameIndex(
                name: "IX_ExternalIds_MetadataId",
                table: "ExternalIds",
                newName: "IX_ExternalIds_MediaId");

            migrationBuilder.AddColumn<string[]>(
                name: "Genres",
                table: "Medias",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "OriginalLanguage",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalTitle",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Overview",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReleaseDate",
                table: "Medias",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tagline",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Medias",
                type: "text",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalIds_Medias_MediaId",
                table: "ExternalIds",
                column: "MediaId",
                principalTable: "Medias",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataPictures_Medias_MediaId",
                table: "MetadataPictures",
                column: "MediaId",
                principalTable: "Medias",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PersonRoles_Medias_MediaId",
                table: "PersonRoles",
                column: "MediaId",
                principalTable: "Medias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ratings_Medias_MediaId",
                table: "Ratings",
                column: "MediaId",
                principalTable: "Medias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalIds_Medias_MediaId",
                table: "ExternalIds");

            migrationBuilder.DropForeignKey(
                name: "FK_MetadataPictures_Medias_MediaId",
                table: "MetadataPictures");

            migrationBuilder.DropForeignKey(
                name: "FK_PersonRoles_Medias_MediaId",
                table: "PersonRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_Ratings_Medias_MediaId",
                table: "Ratings");

            migrationBuilder.DropColumn(
                name: "Genres",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "OriginalLanguage",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "OriginalTitle",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Overview",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "ReleaseDate",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Tagline",
                table: "Medias");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Medias");

            migrationBuilder.RenameColumn(
                name: "MediaId",
                table: "Ratings",
                newName: "MetadataId");

            migrationBuilder.RenameIndex(
                name: "IX_Ratings_MediaId",
                table: "Ratings",
                newName: "IX_Ratings_MetadataId");

            migrationBuilder.RenameColumn(
                name: "MediaId",
                table: "PersonRoles",
                newName: "MetadataId");

            migrationBuilder.RenameIndex(
                name: "IX_PersonRoles_MediaId",
                table: "PersonRoles",
                newName: "IX_PersonRoles_MetadataId");

            migrationBuilder.RenameColumn(
                name: "MediaId",
                table: "MetadataPictures",
                newName: "MetadataId");

            migrationBuilder.RenameIndex(
                name: "IX_MetadataPictures_MediaId",
                table: "MetadataPictures",
                newName: "IX_MetadataPictures_MetadataId");

            migrationBuilder.RenameColumn(
                name: "MediaId",
                table: "ExternalIds",
                newName: "MetadataId");

            migrationBuilder.RenameIndex(
                name: "IX_ExternalIds_MediaId",
                table: "ExternalIds",
                newName: "IX_ExternalIds_MetadataId");

            migrationBuilder.CreateTable(
                name: "MediaMetadatas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    Genres = table.Column<string[]>(type: "text[]", nullable: false),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true),
                    OriginalTitle = table.Column<string>(type: "text", nullable: false),
                    ReleaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    OriginalLanguage = table.Column<string>(type: "text", nullable: true),
                    Overview = table.Column<string>(type: "text", nullable: true),
                    TagLine = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaMetadatas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaMetadatas_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaMetadatas_MediaId",
                table: "MediaMetadatas",
                column: "MediaId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalIds_MediaMetadatas_MetadataId",
                table: "ExternalIds",
                column: "MetadataId",
                principalTable: "MediaMetadatas",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MetadataPictures_MediaMetadatas_MetadataId",
                table: "MetadataPictures",
                column: "MetadataId",
                principalTable: "MediaMetadatas",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PersonRoles_MediaMetadatas_MetadataId",
                table: "PersonRoles",
                column: "MetadataId",
                principalTable: "MediaMetadatas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ratings_MediaMetadatas_MetadataId",
                table: "Ratings",
                column: "MetadataId",
                principalTable: "MediaMetadatas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
