using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesSaaS.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeFiscalWebhooksAndUserWarehouses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "Products",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 21m);

            migrationBuilder.AddColumn<Guid>(
                name: "AssociatedInvoiceId",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ExternalEventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserWarehouses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWarehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWarehouses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserWarehouses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserWarehouses_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Preserve the access current operational users had before branch
            // permissions became explicit. Administrators remain tenant-wide.
            migrationBuilder.Sql("""
                INSERT INTO UserWarehouses (Id, UserId, TenantId, WarehouseId, CreatedAtUtc)
                SELECT NEWID(), membership.UserId, membership.TenantId, warehouse.Id, SYSUTCDATETIME()
                FROM TenantMemberships membership
                INNER JOIN Warehouses warehouse ON warehouse.TenantId = membership.TenantId AND warehouse.IsActive = CAST(1 AS bit)
                WHERE membership.IsActive = CAST(1 AS bit)
                  AND membership.Role IN ('Seller', 'Warehouse');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_AssociatedInvoiceId",
                table: "Invoices",
                column: "AssociatedInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_Provider_EventType_ExternalEventId",
                table: "PaymentWebhookEvents",
                columns: new[] { "Provider", "EventType", "ExternalEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWarehouses_TenantId_WarehouseId",
                table: "UserWarehouses",
                columns: new[] { "TenantId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserWarehouses_UserId_TenantId_WarehouseId",
                table: "UserWarehouses",
                columns: new[] { "UserId", "TenantId", "WarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWarehouses_WarehouseId",
                table: "UserWarehouses",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Invoices_AssociatedInvoiceId",
                table: "Invoices",
                column: "AssociatedInvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Invoices_AssociatedInvoiceId",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "PaymentWebhookEvents");

            migrationBuilder.DropTable(
                name: "UserWarehouses");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_AssociatedInvoiceId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AssociatedInvoiceId",
                table: "Invoices");
        }
    }
}
