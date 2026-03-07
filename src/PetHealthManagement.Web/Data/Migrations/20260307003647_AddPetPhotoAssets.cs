using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetHealthManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPetPhotoAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PhotoImageId",
                table: "Pets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImageAssets",
                columns: table => new
                {
                    ImageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageAssets", x => x.ImageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageAssets_CreatedAt",
                table: "ImageAssets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ImageAssets_OwnerId_Status",
                table: "ImageAssets",
                columns: new[] { "OwnerId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImageAssets");

            migrationBuilder.DropColumn(
                name: "PhotoImageId",
                table: "Pets");
        }
    }
}
