using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesSaaS.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseIdToAccountsAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "CustomerAccountEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "AuditLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountEntries_TenantId_WarehouseId_CustomerId_OccurredAtUtc",
                table: "CustomerAccountEntries",
                columns: new[] { "TenantId", "WarehouseId", "CustomerId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccountEntries_WarehouseId",
                table: "CustomerAccountEntries",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_WarehouseId_TimestampUtc",
                table: "AuditLogs",
                columns: new[] { "TenantId", "WarehouseId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_WarehouseId",
                table: "AuditLogs",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Warehouses_WarehouseId",
                table: "AuditLogs",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerAccountEntries_Warehouses_WarehouseId",
                table: "CustomerAccountEntries",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Warehouses_WarehouseId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerAccountEntries_Warehouses_WarehouseId",
                table: "CustomerAccountEntries");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAccountEntries_TenantId_WarehouseId_CustomerId_OccurredAtUtc",
                table: "CustomerAccountEntries");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAccountEntries_WarehouseId",
                table: "CustomerAccountEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TenantId_WarehouseId_TimestampUtc",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_WarehouseId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "CustomerAccountEntries");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "AuditLogs");
        }
    }
}
