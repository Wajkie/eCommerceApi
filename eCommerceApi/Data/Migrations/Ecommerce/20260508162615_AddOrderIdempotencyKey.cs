using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerceApi.Data.Migrations.Ecommerce
{
    /// <inheritdoc />
    public partial class AddOrderIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IdempotencyKey",
                table: "Orders",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StoreId_IdempotencyKey",
                table: "Orders",
                columns: new[] { "StoreId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_StoreId_IdempotencyKey",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Orders");
        }
    }
}
