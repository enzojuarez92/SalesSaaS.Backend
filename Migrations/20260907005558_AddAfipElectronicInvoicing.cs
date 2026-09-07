using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesSaaS.Migrations
{
    /// <inheritdoc />
    public partial class AddAfipElectronicInvoicing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AfipErrors",
                table: "Invoices",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AfipResult",
                table: "Invoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AfipSalesPoint",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AfipVoucherType",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BarCode",
                table: "Invoices",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cae",
                table: "Invoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CaeExpirationDate",
                table: "Invoices",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantFiscalProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuerTaxId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CertificateContentEncrypted = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    PrivateKeyContentEncrypted = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    CertificatePassphraseEncrypted = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    CertificateAlias = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsPfxCertificate = table.Column<bool>(type: "bit", nullable: false),
                    Environment = table.Column<int>(type: "int", nullable: false),
                    SalesPoint = table.Column<int>(type: "int", nullable: false),
                    DefaultConcept = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantFiscalProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantFiscalProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantFiscalProfiles_TenantId",
                table: "TenantFiscalProfiles",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantFiscalProfiles");

            migrationBuilder.DropColumn(
                name: "AfipErrors",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "AfipResult",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "AfipSalesPoint",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "AfipVoucherType",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BarCode",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Cae",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CaeExpirationDate",
                table: "Invoices");
        }
    }
}
