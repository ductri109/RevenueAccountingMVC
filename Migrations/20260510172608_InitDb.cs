using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RevenueAccountingMVC.Migrations
{
    /// <inheritdoc />
    public partial class InitDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    ParentAccountId = table.Column<int>(type: "int", nullable: true),
                    Nature = table.Column<int>(type: "int", nullable: false),
                    IsDetail = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Accounts_Accounts_ParentAccountId",
                        column: x => x.ParentAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerType = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TaxCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceivableAccountId = table.Column<int>(type: "int", nullable: true),
                    MaxDebtDays = table.Column<int>(type: "int", nullable: true),
                    ContactPerson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Customers_Accounts_ReceivableAccountId",
                        column: x => x.ReceivableAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoucherType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VoucherId = table.Column<int>(type: "int", nullable: false),
                    VoucherCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VoucherDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TaxId = table.Column<int>(type: "int", nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PostingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReversalEntryId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntries_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Taxes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaxCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TaxName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecondaryName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsDeductible = table.Column<bool>(type: "bit", nullable: false),
                    TaxAccountId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Taxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Taxes_Accounts_TaxAccountId",
                        column: x => x.TaxAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesVouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoucherCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
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
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductType = table.Column<int>(type: "int", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RevenueAccountId = table.Column<int>(type: "int", nullable: true),
                    InventoryAccountId = table.Column<int>(type: "int", nullable: true),
                    TaxId = table.Column<int>(type: "int", nullable: true),
                    DefaultUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DefaultTaxRate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Accounts_InventoryAccountId",
                        column: x => x.InventoryAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Products_Accounts_RevenueAccountId",
                        column: x => x.RevenueAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Products_Taxes_TaxId",
                        column: x => x.TaxId,
                        principalTable: "Taxes",
                        principalColumn: "Id");
                });

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
                name: "RevenueByProductReportLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AverageUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalTax = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPayment = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdjustmentQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdjustmentRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CustomerCount = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevenueByProductReportLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevenueByProductReportLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RevenueReportLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalTax = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPayment = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdjustmentQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdjustmentRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdjustmentTax = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CustomerCount = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevenueReportLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevenueReportLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
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
                name: "IX_Accounts_ParentAccountId",
                table: "Accounts",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_ReceivableAccountId",
                table: "Customers",
                column: "ReceivableAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_AccountId",
                table: "JournalEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_InventoryAccountId",
                table: "Products",
                column: "InventoryAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_RevenueAccountId",
                table: "Products",
                column: "RevenueAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_TaxId",
                table: "Products",
                column: "TaxId");

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

            migrationBuilder.CreateIndex(
                name: "IX_RevenueByProductReportLines_ProductId",
                table: "RevenueByProductReportLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueReportLines_ProductId",
                table: "RevenueReportLines",
                column: "ProductId");

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

            migrationBuilder.CreateIndex(
                name: "IX_Taxes_TaxAccountId",
                table: "Taxes",
                column: "TaxAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalEntries");

            migrationBuilder.DropTable(
                name: "RevenueAdjustmentDetails");

            migrationBuilder.DropTable(
                name: "RevenueByProductReportLines");

            migrationBuilder.DropTable(
                name: "RevenueReportLines");

            migrationBuilder.DropTable(
                name: "SalesVoucherDetails");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "RevenueAdjustments");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "SalesVouchers");

            migrationBuilder.DropTable(
                name: "Taxes");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "Accounts");
        }
    }
}
