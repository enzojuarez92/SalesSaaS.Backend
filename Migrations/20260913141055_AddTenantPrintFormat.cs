using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesSaaS.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPrintFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrintFormat",
                table: "Tenants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "a4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrintFormat",
                table: "Tenants");
        }
    }
}
