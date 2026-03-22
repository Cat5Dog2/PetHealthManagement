using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetHealthManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Visits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PetId = table.Column<int>(type: "int", nullable: false),
                    VisitDate = table.Column<DateTime>(type: "date", nullable: false),
                    ClinicName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Diagnosis = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Prescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Visits_Pets_PetId",
                        column: x => x.PetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VisitImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VisitId = table.Column<int>(type: "int", nullable: false),
                    ImageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitImages_ImageAssets_ImageId",
                        column: x => x.ImageId,
                        principalTable: "ImageAssets",
                        principalColumn: "ImageId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VisitImages_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VisitImages_ImageId",
                table: "VisitImages",
                column: "ImageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisitImages_VisitId_SortOrder",
                table: "VisitImages",
                columns: new[] { "VisitId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Visits_PetId_VisitDate_Id",
                table: "Visits",
                columns: new[] { "PetId", "VisitDate", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VisitImages");

            migrationBuilder.DropTable(
                name: "Visits");
        }
    }
}
