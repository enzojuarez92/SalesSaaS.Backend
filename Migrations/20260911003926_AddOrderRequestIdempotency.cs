using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesSaaS.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderRequestIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId",
                table: "Orders");

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "Orders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId_RequestId",
                table: "Orders",
                columns: new[] { "TenantId", "RequestId" },
                unique: true,
                filter: "[RequestId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_TenantId_RequestId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TenantId",
                table: "Orders",
                column: "TenantId");
        }
    }
}
