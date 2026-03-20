using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetHealthManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHealthLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HealthLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PetId = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WeightKg = table.Column<double>(type: "float", nullable: true),
                    FoodAmountGram = table.Column<int>(type: "int", nullable: true),
                    WalkMinutes = table.Column<int>(type: "int", nullable: true),
                    StoolCondition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HealthLogs_Pets_PetId",
                        column: x => x.PetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HealthLogImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HealthLogId = table.Column<int>(type: "int", nullable: false),
                    ImageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthLogImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HealthLogImages_HealthLogs_HealthLogId",
                        column: x => x.HealthLogId,
                        principalTable: "HealthLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HealthLogImages_ImageAssets_ImageId",
                        column: x => x.ImageId,
                        principalTable: "ImageAssets",
                        principalColumn: "ImageId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HealthLogImages_HealthLogId_SortOrder",
                table: "HealthLogImages",
                columns: new[] { "HealthLogId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HealthLogImages_ImageId",
                table: "HealthLogImages",
                column: "ImageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HealthLogs_PetId_RecordedAt_Id",
                table: "HealthLogs",
                columns: new[] { "PetId", "RecordedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HealthLogImages");

            migrationBuilder.DropTable(
                name: "HealthLogs");
        }
    }
}
