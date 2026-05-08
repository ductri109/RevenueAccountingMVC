# Hướng dẫn Import Dữ liệu

## Thứ tự import (quan trọng!)

1. **Accounts.csv** - Tài khoản (nền tảng)
2. **Taxes.csv** - Thuế (cần Account)
3. **Customers.csv** - Khách hàng (cần Account)
4. **Products.csv** - Sản phẩm (cần Account + Tax)

---

## Chi tiết từng file

### 1. Accounts.csv (20 tài khoản)
| Cột | Mô tả | Ví dụ |
|-----|-------|-------|
| AccountNumber | Số tài khoản (duy nhất) | 1111 |
| AccountName | Tên tài khoản | Tiền mặt |
| Category | 1=Tài sản, 2=Nợ&Vốn, 3=Doanh thu, 4=Chi phí | 1 |
| Nature | 1=Dư Nợ, 2=Dư Có, 3=Lưỡng tính, 4=Không dư | 1 |
| IsDetail | TRUE = tài khoản chi tiết | TRUE |
| Status | 1=Hoạt động, 2=Ngừng | 1 |
| ParentAccountId | ID tài khoản cha (để trống nếu không có) | (trống) |
| Description | Mô tả thêm | Tiền mặt tại quỹ |

### 2. Taxes.csv (10 loại thuế)
| Cột | Mô tả | Ví dụ |
|-----|-------|-------|
| TaxCode | Mã thuế (duy nhất) | R001 |
| TaxName | Tên thuế | Thuế GTGT 10% |
| SecondaryName | Tên phụ | VAT 10% |
| TaxRate | % thuế | 10 |
| IsDeductible | Khấu trừ được? | TRUE |
| TaxAccountId | FK → Account.Id (dùng cột 6 - 1331) | 6 |
| Status | 1=Hoạt động | 1 |

### 3. Customers.csv (20 khách hàng)
| Cột | Mô tả | Ví dụ |
|-----|-------|-------|
| CustomerCode | Mã KH (duy nhất) | KH0001 |
| CustomerName | Tên khách hàng | Công ty TNHH ABC |
| CustomerType | 1=Cá nhân, 2=Doanh nghiệp | 2 |
| Address | Địa chỉ | 123 Nguyễn Trãi, Q1, TP.HCM |
| PhoneNumber | Điện thoại | 0901234567 |
| Email | Email | info@abc.com |
| TaxCode | Mã số thuế | 1234567890 |
| ReceivableAccountId | FK → Account.Id (dùng cột 3 - 1311) | 3 |
| MaxDebtDays | Số ngày nợ tối đa | 30 |
| ContactPerson | Người liên hệ | Nguyễn Văn A |
| Status | 1=Hoạt động | 1 |

### 4. Products.csv (20 sản phẩm)
| Cột | Mô tả | Ví dụ |
|-----|-------|-------|
| ProductCode | Mã SP (duy nhất) | SP001 |
| ProductName | Tên sản phẩm | Gạo tẻ thơm |
| ProductType | 1=Hàng hóa, 2=Dịch vụ | 1 |
| Unit | Đơn vị tính | Kg |
| Status | 1=Không hoạt động, 2=Hoạt động | 2 |
| RevenueAccountId | FK → Account.Id (doanh thu - cột 4) | 4 |
| InventoryAccountId | FK → Account.Id (kho - cột 15) | 15 |
| TaxId | FK → Tax.Id (cột 1 trong Taxes.csv) | 1 |
| DefaultUnitPrice | Giá mặc định | 25000 |
| DefaultTaxRate | % thuế mặc định | 10 |
| Description | Mô tả | Gạo tẻ thơm loại 1 |

---

## Cách import trong SQL Server

```sql
-- 1. Import Accounts
BULK INSERT Accounts FROM 'C:\path\to\Accounts.csv'
WITH (FIRSTROW = 2, FIELDTERMINATOR = ',', ROWTERMINATOR = '\n');

-- 2. Import Taxes
BULK INSERT Taxes FROM 'C:\path\to\Taxes.csv'
WITH (FIRSTROW = 2, FIELDTERMINATOR = ',', ROWTERMINATOR = '\n');

-- 3. Import Customers
BULK INSERT Customers FROM 'C:\path\to\Customers.csv'
WITH (FIRSTROW = 2, FIELDTERMINATOR = ',', ROWTERMINATOR = '\n');

-- 4. Import Products
BULK INSERT Products FROM 'C:\path\to\Products.csv'
WITH (FIRSTROW = 2, FIELDTERMINATOR = ',', ROWTERMINATOR = '\n');
```

## Lưu ý quan trọng

- **Thứ tự import phải đúng**: Account → Tax → Customer → Product
- **FK phải tồn tại**: Kiểm tra ID tài khoản, thuế trước khi import
- **Encoding**: Nên dùng UTF-8 không BOM
- **NULL**: Dùng từ khóa NULL (không có dấu nháy) cho giá trị rỗng