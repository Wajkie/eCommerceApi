using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eCommerceApi.Data.Migrations.Central
{
    /// <inheritdoc />
    public partial class RenameApiKeyToApiKeyHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ApiKey",
                table: "Stores",
                newName: "ApiKeyHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ApiKeyHash",
                table: "Stores",
                newName: "ApiKey");
        }
    }
}
