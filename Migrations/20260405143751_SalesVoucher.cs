using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RevenueAccountingMVC.Migrations
{
    /// <inheritdoc />
    public partial class SalesVoucher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesVouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoucherCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AccountingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    CustomerNameSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerAddressSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DebtDays = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalTaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPayment = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesVouchers_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesVoucherDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesVoucherId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductNameSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_SalesVoucherDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesVoucherDetails_Accounts_CreditAccountId",
                        column: x => x.CreditAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SalesVoucherDetails_Accounts_DebitAccountId",
                        column: x => x.DebitAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SalesVoucherDetails_Accounts_TaxAccountId",
                        column: x => x.TaxAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SalesVoucherDetails_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesVoucherDetails_SalesVouchers_SalesVoucherId",
                        column: x => x.SalesVoucherId,
                        principalTable: "SalesVouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesVoucherDetails_Taxes_TaxId",
                        column: x => x.TaxId,
                        principalTable: "Taxes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesVoucherDetails_CreditAccountId",
                table: "SalesVoucherDetails",
                column: "CreditAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesVoucherDetails_DebitAccountId",
                table: "SalesVoucherDetails",
                column: "DebitAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesVoucherDetails_ProductId",
                table: "SalesVoucherDetails",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesVoucherDetails_SalesVoucherId",
                table: "SalesVoucherDetails",
                column: "SalesVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesVoucherDetails_TaxAccountId",
                table: "SalesVoucherDetails",
                column: "TaxAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesVoucherDetails_TaxId",
                table: "SalesVoucherDetails",
                column: "TaxId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesVouchers_CustomerId",
                table: "SalesVouchers",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesVoucherDetails");

            migrationBuilder.DropTable(
                name: "SalesVouchers");
        }
    }
}
