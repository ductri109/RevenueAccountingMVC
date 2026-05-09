-- Kiểm tra dữ liệu Seed

-- Đếm SalesVouchers
SELECT COUNT(*) AS 'Tổng SalesVouchers' FROM SalesVouchers;

-- Đếm RevenueAdjustments
SELECT COUNT(*) AS 'Tổng RevenueAdjustments' FROM RevenueAdjustments;

-- Đếm JournalEntries
SELECT COUNT(*) AS 'Tổng JournalEntries' FROM JournalEntries;

-- Xem 5 SalesVoucher mới nhất
SELECT TOP 5 
    Id, 
    VoucherCode, 
    AccountingDate, 
    TotalAmount, 
    TotalTaxAmount, 
    TotalPayment,
    CreatedAt
FROM SalesVouchers 
ORDER BY CreatedAt DESC;

-- Xem 5 RevenueAdjustment mới nhất
SELECT TOP 5 
    Id, 
    AdjustmentCode, 
    AccountingDate, 
    TotalDiscountAmount, 
    TotalTaxAmount, 
    TotalPayment,
    CreatedAt
FROM RevenueAdjustments 
ORDER BY CreatedAt DESC;

-- Kiểm tra bút toán (JournalEntry)
SELECT TOP 10
    Id,
    VoucherType,
    VoucherCode,
    VoucherDate,
    Account.AccountNumber,
    EntryType,
    Amount,
    Description
FROM JournalEntries
INNER JOIN Accounts ON JournalEntries.AccountId = Accounts.Id
ORDER BY VoucherDate DESC, Id DESC;

-- Kiểm tra phạm vi ngày dữ liệu
SELECT 
    MIN(AccountingDate) AS 'Ngày sớm nhất',
    MAX(AccountingDate) AS 'Ngày muộn nhất',
    DATEDIFF(DAY, MIN(AccountingDate), MAX(AccountingDate)) AS 'Số ngày khoảng'
FROM SalesVouchers;
