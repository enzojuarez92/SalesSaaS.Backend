using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesSaaS.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "PurchaseOrders",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "PurchaseOrders");
        }
    }
}
