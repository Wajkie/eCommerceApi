using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerceApi.Data.Migrations.Ecommerce
{
    /// <inheritdoc />
    public partial class AddStoreIdToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_ExternalId",
                table: "Products");

            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                table: "Products",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Products_StoreId",
                table: "Products",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_StoreId_ExternalId",
                table: "Products",
                columns: new[] { "StoreId", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_StoreId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_StoreId_ExternalId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ExternalId",
                table: "Products",
                column: "ExternalId",
                unique: true);
        }
    }
}
