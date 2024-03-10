using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaServer.Infrastructure.DatabaseProviders.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaPictures_Metadatas_MovieMetadataId",
                table: "MediaPictures");

            migrationBuilder.DropForeignKey(
                name: "FK_Ratings_Medias_MediaId",
                table: "Ratings");

            migrationBuilder.DropIndex(
                name: "IX_MediaPictures_MovieMetadataId",
                table: "MediaPictures");

            migrationBuilder.DropColumn(
                name: "MovieMetadataId",
                table: "MediaPictures");

            migrationBuilder.DropColumn(
                name: "IsIdentified",
                table: "IndexedFiles");

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
                table: "MediaPictures",
                newName: "MetadataId");

            migrationBuilder.AddColumn<int>(
                name: "BaseMetadataId",
                table: "ExternalIds",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaPictures_MetadataId",
                table: "MediaPictures",
                column: "MetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIds_BaseMetadataId",
                table: "ExternalIds",
                column: "BaseMetadataId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalIds_Metadatas_BaseMetadataId",
                table: "ExternalIds",
                column: "BaseMetadataId",
                principalTable: "Metadatas",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaPictures_Metadatas_MetadataId",
                table: "MediaPictures",
                column: "MetadataId",
                principalTable: "Metadatas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ratings_Metadatas_MetadataId",
                table: "Ratings",
                column: "MetadataId",
                principalTable: "Metadatas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalIds_Metadatas_BaseMetadataId",
                table: "ExternalIds");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaPictures_Metadatas_MetadataId",
                table: "MediaPictures");

            migrationBuilder.DropForeignKey(
                name: "FK_Ratings_Metadatas_MetadataId",
                table: "Ratings");

            migrationBuilder.DropIndex(
                name: "IX_MediaPictures_MetadataId",
                table: "MediaPictures");

            migrationBuilder.DropIndex(
                name: "IX_ExternalIds_BaseMetadataId",
                table: "ExternalIds");

            migrationBuilder.DropColumn(
                name: "BaseMetadataId",
                table: "ExternalIds");

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
                table: "MediaPictures",
                newName: "MediaId");

            migrationBuilder.AddColumn<int>(
                name: "MovieMetadataId",
                table: "MediaPictures",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsIdentified",
                table: "IndexedFiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_MediaPictures_MovieMetadataId",
                table: "MediaPictures",
                column: "MovieMetadataId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaPictures_Metadatas_MovieMetadataId",
                table: "MediaPictures",
                column: "MovieMetadataId",
                principalTable: "Metadatas",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Ratings_Medias_MediaId",
                table: "Ratings",
                column: "MediaId",
                principalTable: "Medias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
