using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetHealthManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixMissingUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VisitImages_VisitId_SortOrder",
                table: "VisitImages");

            migrationBuilder.DropIndex(
                name: "IX_HealthLogImages_HealthLogId_SortOrder",
                table: "HealthLogImages");

            migrationBuilder.CreateIndex(
                name: "IX_VisitImages_VisitId_SortOrder",
                table: "VisitImages",
                columns: new[] { "VisitId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pets_Name",
                table: "Pets",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ImageAssets_StorageKey",
                table: "ImageAssets",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HealthLogImages_HealthLogId_SortOrder",
                table: "HealthLogImages",
                columns: new[] { "HealthLogId", "SortOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VisitImages_VisitId_SortOrder",
                table: "VisitImages");

            migrationBuilder.DropIndex(
                name: "IX_Pets_Name",
                table: "Pets");

            migrationBuilder.DropIndex(
                name: "IX_ImageAssets_StorageKey",
                table: "ImageAssets");

            migrationBuilder.DropIndex(
                name: "IX_HealthLogImages_HealthLogId_SortOrder",
                table: "HealthLogImages");

            migrationBuilder.CreateIndex(
                name: "IX_VisitImages_VisitId_SortOrder",
                table: "VisitImages",
                columns: new[] { "VisitId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HealthLogImages_HealthLogId_SortOrder",
                table: "HealthLogImages",
                columns: new[] { "HealthLogId", "SortOrder" });
        }
    }
}
