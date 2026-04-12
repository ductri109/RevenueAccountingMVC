using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RevenueAccountingMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddRevenueAdjustment_UC3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "RevenueAdjustments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdjustmentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AdjustmentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AccountingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalSalesVoucherId = table.Column<int>(type: "int", nullable: false),
                    TotalDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalTaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPayment = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevenueAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevenueAdjustments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RevenueAdjustments_SalesVouchers_OriginalSalesVoucherId",
                        column: x => x.OriginalSalesVoucherId,
                        principalTable: "SalesVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RevenueAdjustmentDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RevenueAdjustmentId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    AdjustmentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DebitAccountId = table.Column<int>(type: "int", nullable: true),
                    CreditAccountId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxId = table.Column<int>(type: "int", nullable: true),
                    TaxRateSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAccountId = table.Column<int>(type: "int", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevenueAdjustmentDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevenueAdjustmentDetails_Accounts_CreditAccountId",
                        column: x => x.CreditAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RevenueAdjustmentDetails_Accounts_DebitAccountId",
                        column: x => x.DebitAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RevenueAdjustmentDetails_Accounts_TaxAccountId",
                        column: x => x.TaxAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RevenueAdjustmentDetails_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RevenueAdjustmentDetails_RevenueAdjustments_RevenueAdjustmentId",
                        column: x => x.RevenueAdjustmentId,
                        principalTable: "RevenueAdjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RevenueAdjustmentDetails_Taxes_TaxId",
                        column: x => x.TaxId,
                        principalTable: "Taxes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RevenueAdjustmentDetails_CreditAccountId",
                table: "RevenueAdjustmentDetails",
                column: "CreditAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueAdjustmentDetails_DebitAccountId",
                table: "RevenueAdjustmentDetails",
                column: "DebitAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueAdjustmentDetails_ProductId",
                table: "RevenueAdjustmentDetails",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueAdjustmentDetails_RevenueAdjustmentId",
                table: "RevenueAdjustmentDetails",
                column: "RevenueAdjustmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueAdjustmentDetails_TaxAccountId",
                table: "RevenueAdjustmentDetails",
                column: "TaxAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueAdjustmentDetails_TaxId",
                table: "RevenueAdjustmentDetails",
                column: "TaxId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueAdjustments_CustomerId",
                table: "RevenueAdjustments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueAdjustments_OriginalSalesVoucherId",
                table: "RevenueAdjustments",
                column: "OriginalSalesVoucherId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RevenueAdjustmentDetails");

            migrationBuilder.DropTable(
                name: "RevenueAdjustments");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
