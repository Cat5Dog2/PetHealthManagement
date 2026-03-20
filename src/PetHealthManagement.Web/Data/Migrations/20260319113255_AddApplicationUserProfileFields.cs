using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetHealthManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationUserProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AvatarImageId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "AspNetUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AspNetUsers",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UsedImageBytes",
                table: "AspNetUsers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(
                """
                UPDATE U
                SET U.DisplayName = LEFT(COALESCE(NULLIF(U.UserName, N''), NULLIF(U.Email, N''), U.Id), 50)
                FROM AspNetUsers AS U
                WHERE U.DisplayName = N'';
                """);

            migrationBuilder.Sql(
                """
                UPDATE U
                SET U.UsedImageBytes = ISNULL(S.TotalSizeBytes, 0)
                FROM AspNetUsers AS U
                OUTER APPLY
                (
                    SELECT SUM(IA.SizeBytes) AS TotalSizeBytes
                    FROM ImageAssets AS IA
                    WHERE IA.OwnerId = U.Id
                      AND IA.Status = N'Ready'
                ) AS S;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_AvatarImageId",
                table: "AspNetUsers",
                column: "AvatarImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_ImageAssets_AvatarImageId",
                table: "AspNetUsers",
                column: "AvatarImageId",
                principalTable: "ImageAssets",
                principalColumn: "ImageId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_ImageAssets_AvatarImageId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_AvatarImageId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AvatarImageId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UsedImageBytes",
                table: "AspNetUsers");
        }
    }
}
