using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetHealthManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRowVersionConcurrencyTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Visits",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ScheduleItems",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Pets",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "HealthLogs",
                type: "rowversion",
                rowVersion: true,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ScheduleItems");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Pets");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "HealthLogs");
        }
    }
}
