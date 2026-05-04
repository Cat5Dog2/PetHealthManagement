using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetHealthManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPetProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AdoptedDate",
                table: "Pets",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BirthDate",
                table: "Pets",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sex",
                table: "Pets",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdoptedDate",
                table: "Pets");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "Pets");

            migrationBuilder.DropColumn(
                name: "Sex",
                table: "Pets");
        }
    }
}
