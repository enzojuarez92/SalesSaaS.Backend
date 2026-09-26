using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesSaaS.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportImpersonationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImpersonatorUserId",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupportImpersonationLogId",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupportImpersonationLogId",
                table: "AuditLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupportImpersonationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuperAdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImpersonatedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportImpersonationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportImpersonationLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportImpersonationLogs_Users_ImpersonatedUserId",
                        column: x => x.ImpersonatedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportImpersonationLogs_Users_SuperAdminUserId",
                        column: x => x.SuperAdminUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_SupportImpersonationLogId",
                table: "RefreshTokens",
                column: "SupportImpersonationLogId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_SupportImpersonationLogId",
                table: "AuditLogs",
                column: "SupportImpersonationLogId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportImpersonationLogs_EndedAtUtc",
                table: "SupportImpersonationLogs",
                column: "EndedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SupportImpersonationLogs_ImpersonatedUserId",
                table: "SupportImpersonationLogs",
                column: "ImpersonatedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportImpersonationLogs_SuperAdminUserId_StartedAtUtc",
                table: "SupportImpersonationLogs",
                columns: new[] { "SuperAdminUserId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportImpersonationLogs_TenantId_StartedAtUtc",
                table: "SupportImpersonationLogs",
                columns: new[] { "TenantId", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupportImpersonationLogs");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_SupportImpersonationLogId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_SupportImpersonationLogId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ImpersonatorUserId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SupportImpersonationLogId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SupportImpersonationLogId",
                table: "AuditLogs");
        }
    }
}
