USE [master]
GO

IF DB_ID(N'TheSqlODataMcp_TestCatalog') IS NOT NULL
BEGIN
    ALTER DATABASE [TheSqlODataMcp_TestCatalog] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [TheSqlODataMcp_TestCatalog];
END;
GO

CREATE DATABASE [TheSqlODataMcp_TestCatalog];
GO
USE [TheSqlODataMcp_TestCatalog]
GO

CREATE SCHEMA crm;
GO
CREATE SCHEMA sales;
GO
CREATE SCHEMA inventory;
GO
CREATE SCHEMA reporting;
GO
CREATE SCHEMA operations;
GO
CREATE SCHEMA archive;
GO
CREATE SCHEMA unsupported;
GO

CREATE TABLE crm.Customers
(
    CustomerId int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    CustomerCode varchar(16) NOT NULL CONSTRAINT UQ_Customers_CustomerCode UNIQUE,
    ParentCustomerId int NULL,
    DisplayName nvarchar(120) NOT NULL,
    Email varchar(254) NULL,
    CountryCode char(2) NULL,
    PreferredCurrency char(3) NULL,
    IsActive bit NOT NULL CONSTRAINT DF_Customers_IsActive DEFAULT (1),
    CreatedDate date NOT NULL CONSTRAINT DF_Customers_CreatedDate DEFAULT ('2024-01-01'),
    CONSTRAINT FK_Customers_Parent FOREIGN KEY (ParentCustomerId) REFERENCES crm.Customers(CustomerId),
    CONSTRAINT CK_Customers_Currency CHECK (PreferredCurrency IS NULL OR PreferredCurrency IN ('EUR', 'USD', 'GBP'))
);
CREATE UNIQUE INDEX UX_Customers_Email_WhenPresent ON crm.Customers(Email) WHERE Email IS NOT NULL;

CREATE TABLE crm.CustomerAddresses
(
    CustomerAddressId int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_CustomerAddresses PRIMARY KEY,
    CustomerId int NOT NULL,
    AddressKind char(1) NOT NULL,
    AddressLine1 nvarchar(160) NOT NULL,
    City nvarchar(80) NOT NULL,
    PostalCode varchar(16) NULL,
    CountryCode char(2) NULL,
    CONSTRAINT UQ_CustomerAddresses_Customer_Kind UNIQUE(CustomerId, AddressKind),
    CONSTRAINT FK_CustomerAddresses_Customer FOREIGN KEY (CustomerId) REFERENCES crm.Customers(CustomerId),
    CONSTRAINT CK_CustomerAddresses_Kind CHECK (AddressKind IN ('B', 'S'))
);

CREATE TABLE inventory.Categories
(
    CategoryId tinyint NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    CategoryName nvarchar(80) NOT NULL CONSTRAINT UQ_Categories_Name UNIQUE,
    IsDiscontinued bit NOT NULL CONSTRAINT DF_Categories_IsDiscontinued DEFAULT (0)
);

CREATE TABLE inventory.Products
(
    ProductId int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    CategoryId tinyint NOT NULL,
    Sku char(12) NOT NULL CONSTRAINT UQ_Products_Sku UNIQUE,
    ProductName nvarchar(120) NOT NULL,
    UnitPrice decimal(19, 4) NOT NULL,
    UnitCost money NOT NULL,
    WeightKg real NULL,
    IsTaxable bit NOT NULL CONSTRAINT DF_Products_IsTaxable DEFAULT (1),
    ProductAttributes nvarchar(max) NULL CONSTRAINT CK_Products_Json CHECK (ProductAttributes IS NULL OR ISJSON(ProductAttributes) = 1),
    CONSTRAINT FK_Products_Category FOREIGN KEY (CategoryId) REFERENCES inventory.Categories(CategoryId),
    CONSTRAINT CK_Products_Price CHECK (UnitPrice >= 0)
);

CREATE TABLE inventory.Warehouses
(
    WarehouseId smallint NOT NULL CONSTRAINT PK_Warehouses PRIMARY KEY,
    WarehouseCode char(4) NOT NULL CONSTRAINT UQ_Warehouses_Code UNIQUE,
    WarehouseName nvarchar(80) NOT NULL,
    TimeZoneName nvarchar(64) NOT NULL
);

CREATE TABLE inventory.StockBalances
(
    ProductId int NOT NULL,
    WarehouseId smallint NOT NULL,
    Quantity bigint NOT NULL,
    ReorderLevel int NOT NULL,
    LastCountedAt datetime2(3) NOT NULL,
    CONSTRAINT PK_StockBalances PRIMARY KEY(ProductId, WarehouseId),
    CONSTRAINT FK_StockBalances_Product FOREIGN KEY(ProductId) REFERENCES inventory.Products(ProductId),
    CONSTRAINT FK_StockBalances_Warehouse FOREIGN KEY(WarehouseId) REFERENCES inventory.Warehouses(WarehouseId)
);

CREATE TABLE sales.Invoices
(
    InvoiceId int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Invoices PRIMARY KEY,
    InvoiceNumber varchar(24) NOT NULL CONSTRAINT UQ_Invoices_Number UNIQUE,
    BillToCustomerId int NOT NULL,
    ShipToCustomerId int NOT NULL,
    BillToAddressKind char(1) NOT NULL CONSTRAINT DF_Invoices_BillKind DEFAULT ('B'),
    ShipToAddressKind char(1) NOT NULL CONSTRAINT DF_Invoices_ShipKind DEFAULT ('S'),
    LegacyCustomerCode varchar(16) NOT NULL,
    InvoiceDate datetime2(3) NOT NULL,
    DueDate smalldatetime NOT NULL,
    CurrencyCode char(3) NULL,
    Subtotal numeric(19, 4) NOT NULL,
    DiscountAmount decimal(19, 4) NOT NULL CONSTRAINT DF_Invoices_Discount DEFAULT (0),
    TaxAmount decimal(19, 4) NOT NULL,
    TotalDue AS CONVERT(decimal(19, 4), Subtotal - DiscountAmount + TaxAmount) PERSISTED,
    SourceSystem nchar(8) NOT NULL CONSTRAINT DF_Invoices_SourceSystem DEFAULT (N'fixture'),
    ExternalReference uniqueidentifier NOT NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT FK_Invoices_BillToCustomer FOREIGN KEY(BillToCustomerId) REFERENCES crm.Customers(CustomerId),
    CONSTRAINT FK_Invoices_ShipToCustomer FOREIGN KEY(ShipToCustomerId) REFERENCES crm.Customers(CustomerId),
    CONSTRAINT FK_Invoices_BillToAddress FOREIGN KEY(BillToCustomerId, BillToAddressKind) REFERENCES crm.CustomerAddresses(CustomerId, AddressKind),
    CONSTRAINT FK_Invoices_ShipToAddress FOREIGN KEY(ShipToCustomerId, ShipToAddressKind) REFERENCES crm.CustomerAddresses(CustomerId, AddressKind),
    CONSTRAINT CK_Invoices_Amounts CHECK (Subtotal >= DiscountAmount AND DiscountAmount >= 0 AND TaxAmount >= 0)
);

CREATE TABLE sales.InvoiceLines
(
    InvoiceLineId bigint IDENTITY(1, 1) NOT NULL CONSTRAINT PK_InvoiceLines PRIMARY KEY,
    InvoiceId int NOT NULL,
    LineNumber smallint NOT NULL,
    ProductId int NOT NULL,
    Quantity tinyint NOT NULL,
    UnitPrice decimal(19, 4) NOT NULL,
    DiscountPercent float NULL,
    LineTotal AS CONVERT(decimal(19, 4), Quantity * UnitPrice * (1 - ISNULL(DiscountPercent, 0))) PERSISTED,
    CONSTRAINT UQ_InvoiceLines_Invoice_Line UNIQUE(InvoiceId, LineNumber),
    CONSTRAINT FK_InvoiceLines_Invoice FOREIGN KEY(InvoiceId) REFERENCES sales.Invoices(InvoiceId),
    CONSTRAINT FK_InvoiceLines_Product FOREIGN KEY(ProductId) REFERENCES inventory.Products(ProductId),
    CONSTRAINT CK_InvoiceLines_Quantity CHECK (Quantity > 0)
);

CREATE TABLE sales.Payments
(
    PaymentId int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Payments PRIMARY KEY,
    InvoiceId int NOT NULL CONSTRAINT UQ_Payments_Invoice UNIQUE,
    PaidAt datetimeoffset(0) NOT NULL,
    AmountPaid money NOT NULL,
    PaymentReference varchar(32) NULL,
    CONSTRAINT FK_Payments_Invoice FOREIGN KEY(InvoiceId) REFERENCES sales.Invoices(InvoiceId),
    CONSTRAINT CK_Payments_Amount CHECK (AmountPaid > 0)
);

CREATE TABLE sales.InvoiceStatuses
(
    InvoiceId int NOT NULL CONSTRAINT PK_InvoiceStatuses PRIMARY KEY,
    StatusCode varchar(16) NOT NULL,
    StatusChangedAt datetime2(3) NOT NULL,
    ValidFrom datetime2 GENERATED ALWAYS AS ROW START NOT NULL,
    ValidTo datetime2 GENERATED ALWAYS AS ROW END NOT NULL,
    PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo),
    CONSTRAINT FK_InvoiceStatuses_Invoice FOREIGN KEY(InvoiceId) REFERENCES sales.Invoices(InvoiceId),
    CONSTRAINT CK_InvoiceStatuses_Code CHECK (StatusCode IN ('Draft', 'Open', 'Paid', 'Overdue'))
) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = sales.InvoiceStatusesHistory));

CREATE INDEX IX_InvoiceLines_Product_Quantity ON sales.InvoiceLines(ProductId, Quantity) INCLUDE (UnitPrice);

CREATE TABLE operations.TypeCoverage
(
    TypeCoverageId int NOT NULL CONSTRAINT PK_TypeCoverage PRIMARY KEY,
    Flag bit NOT NULL,
    TinyValue tinyint NOT NULL,
    SmallValue smallint NOT NULL,
    IntValue int NOT NULL,
    BigValue bigint NOT NULL,
    DecimalValue decimal(18, 4) NOT NULL,
    NumericValue numeric(18, 4) NOT NULL,
    MoneyValue money NOT NULL,
    RealValue real NOT NULL,
    FloatValue float NOT NULL,
    CharValue char(4) NOT NULL,
    VarCharValue varchar(40) NOT NULL,
    NCharValue nchar(4) NOT NULL,
    NVarCharValue nvarchar(40) NOT NULL,
    MaxText varchar(max) NULL,
    JsonText nvarchar(max) NULL CONSTRAINT CK_TypeCoverage_Json CHECK (JsonText IS NULL OR ISJSON(JsonText) = 1),
    GuidValue uniqueidentifier NOT NULL,
    DateValue date NOT NULL,
    TimeValue time(3) NOT NULL,
    DateTimeValue datetime NOT NULL,
    SmallDateTimeValue smalldatetime NOT NULL,
    DateTime2Value datetime2(3) NOT NULL,
    DateTimeOffsetValue datetimeoffset(0) NOT NULL,
    BinaryValue binary(4) NOT NULL,
    VarBinaryValue varbinary(16) NOT NULL,
    XmlValue xml NULL,
    VariantValue sql_variant NULL,
    HierarchyValue hierarchyid NULL,
    VersionValue rowversion NOT NULL
);

CREATE TABLE archive.Invoices
(
    ArchiveInvoiceId int NOT NULL CONSTRAINT PK_ArchiveInvoices PRIMARY KEY,
    InvoiceNumber varchar(24) NOT NULL,
    ArchivedAt datetime2(3) NOT NULL,
    ArchivedReason nvarchar(80) NULL
);
GO

CREATE VIEW reporting.InvoiceDetail AS
SELECT i.InvoiceId, i.InvoiceNumber, i.InvoiceDate, i.CurrencyCode, i.TotalDue,
       b.DisplayName AS BillToCustomerName, s.DisplayName AS ShipToCustomerName,
       l.LineNumber, p.Sku, p.ProductName, l.Quantity, l.LineTotal
FROM sales.Invoices AS i
JOIN crm.Customers AS b ON b.CustomerId = i.BillToCustomerId
JOIN crm.Customers AS s ON s.CustomerId = i.ShipToCustomerId
JOIN sales.InvoiceLines AS l ON l.InvoiceId = i.InvoiceId
JOIN inventory.Products AS p ON p.ProductId = l.ProductId;
GO
CREATE VIEW reporting.InvoiceMonthlySummary AS
SELECT CONVERT(date, DATEFROMPARTS(YEAR(InvoiceDate), MONTH(InvoiceDate), 1)) AS InvoiceMonth,
       CurrencyCode, COUNT_BIG(*) AS InvoiceCount, SUM(TotalDue) AS TotalAmount
FROM sales.Invoices
GROUP BY CONVERT(date, DATEFROMPARTS(YEAR(InvoiceDate), MONTH(InvoiceDate), 1)), CurrencyCode;
GO

CREATE PROCEDURE unsupported.RebuildFixtureCache AS SELECT 1 AS Ignored;
GO
CREATE FUNCTION unsupported.FixtureScalar(@input int) RETURNS int AS BEGIN RETURN @input; END;
GO
CREATE SEQUENCE unsupported.FixtureSequence AS int START WITH 1 INCREMENT BY 1;
GO
CREATE TYPE unsupported.FixtureTableType AS TABLE (ValueId int NOT NULL PRIMARY KEY, ValueName nvarchar(40) NOT NULL);
GO
CREATE SYNONYM unsupported.CustomerAlias FOR crm.Customers;
GO
CREATE TRIGGER crm.UnsupportedNoopCustomerTrigger ON crm.Customers AFTER INSERT AS BEGIN SET NOCOUNT ON; END;
GO

EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Customer relationship data for fixture catalog tests.', @level0type = N'SCHEMA', @level0name = N'crm';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Deterministic sales invoice fixture.', @level0type = N'SCHEMA', @level0name = N'sales', @level1type = N'TABLE', @level1name = N'Invoices';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Invoice total, persisted from component amounts.', @level0type = N'SCHEMA', @level0name = N'sales', @level1type = N'TABLE', @level1name = N'Invoices', @level2type = N'COLUMN', @level2name = N'TotalDue';
EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Keyless invoice-line reporting view.', @level0type = N'SCHEMA', @level0name = N'reporting', @level1type = N'VIEW', @level1name = N'InvoiceDetail';
GO

SET IDENTITY_INSERT crm.Customers ON;
INSERT crm.Customers (CustomerId, CustomerCode, ParentCustomerId, DisplayName, Email, CountryCode, PreferredCurrency, IsActive, CreatedDate)
SELECT n, CONCAT('C', RIGHT(CONCAT('0000', n), 4)), CASE WHEN n = 1 THEN NULL WHEN n % 8 = 0 THEN ((n - 1) / 8) + 1 ELSE NULL END,
       CONCAT(N'Customer ', n), CASE WHEN n % 5 = 0 THEN NULL ELSE CONCAT('customer', n, '@example.test') END,
       CASE n % 4 WHEN 0 THEN 'IT' WHEN 1 THEN 'US' WHEN 2 THEN 'GB' ELSE NULL END,
       CASE n % 4 WHEN 0 THEN 'EUR' WHEN 1 THEN 'USD' WHEN 2 THEN 'GBP' ELSE NULL END,
       CASE WHEN n % 17 = 0 THEN 0 ELSE 1 END, DATEADD(day, n - 1, CONVERT(date, '2024-01-01'))
FROM (SELECT TOP (256) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.all_objects AS a CROSS JOIN sys.all_objects AS b) AS numbers;
SET IDENTITY_INSERT crm.Customers OFF;

SET IDENTITY_INSERT crm.CustomerAddresses ON;
INSERT crm.CustomerAddresses (CustomerAddressId, CustomerId, AddressKind, AddressLine1, City, PostalCode, CountryCode)
SELECT ((c.CustomerId - 1) * 2) + k.KindNumber, c.CustomerId, k.AddressKind,
       CONCAT(N'Fixture Street ', c.CustomerId, N'-', k.AddressKind), CONCAT(N'City ', c.CustomerId % 32),
       CASE WHEN c.CustomerId % 6 = 0 THEN NULL ELSE RIGHT(CONCAT('00000', c.CustomerId), 5) END, c.CountryCode
FROM crm.Customers AS c CROSS JOIN (VALUES (1, 'B'), (2, 'S')) AS k(KindNumber, AddressKind);
SET IDENTITY_INSERT crm.CustomerAddresses OFF;

INSERT inventory.Categories (CategoryId, CategoryName, IsDiscontinued)
SELECT n, CONCAT(N'Category ', n), CASE WHEN n = 12 THEN 1 ELSE 0 END
FROM (SELECT TOP (12) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.all_objects) AS numbers;

SET IDENTITY_INSERT inventory.Products ON;
INSERT inventory.Products (ProductId, CategoryId, Sku, ProductName, UnitPrice, UnitCost, WeightKg, IsTaxable, ProductAttributes)
SELECT n, ((n - 1) % 12) + 1, CONCAT('SKU', RIGHT(CONCAT('000000000', n), 9)), CONCAT(N'Product ', n),
       CONVERT(decimal(19,4), 10 + n / 2.0), CONVERT(money, 5 + n / 3.0), CASE WHEN n % 7 = 0 THEN NULL ELSE CONVERT(real, n / 10.0) END,
       CASE WHEN n % 9 = 0 THEN 0 ELSE 1 END, CASE WHEN n % 4 = 0 THEN NULL ELSE CONCAT(N'{"tier":', n % 5, N'}') END
FROM (SELECT TOP (128) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.all_objects) AS numbers;
SET IDENTITY_INSERT inventory.Products OFF;

INSERT inventory.Warehouses (WarehouseId, WarehouseCode, WarehouseName, TimeZoneName)
VALUES (1, 'ROMA', N'Rome', N'Europe/Rome'), (2, 'LOND', N'London', N'Europe/London'), (3, 'NYC1', N'New York', N'America/New_York'), (4, 'TOKY', N'Tokyo', N'Asia/Tokyo');

INSERT inventory.StockBalances (ProductId, WarehouseId, Quantity, ReorderLevel, LastCountedAt)
SELECT p.ProductId, w.WarehouseId, (p.ProductId * w.WarehouseId) % 97, 10 + (p.ProductId % 20), DATEADD(minute, p.ProductId * w.WarehouseId, CONVERT(datetime2(3), '2024-06-01T08:00:00'))
FROM inventory.Products AS p CROSS JOIN inventory.Warehouses AS w;

SET IDENTITY_INSERT sales.Invoices ON;
INSERT sales.Invoices (InvoiceId, InvoiceNumber, BillToCustomerId, ShipToCustomerId, BillToAddressKind, ShipToAddressKind, LegacyCustomerCode, InvoiceDate, DueDate, CurrencyCode, Subtotal, DiscountAmount, TaxAmount, SourceSystem, ExternalReference)
SELECT n, CONCAT('INV-', RIGHT(CONCAT('000000', n), 6)), ((n - 1) % 256) + 1, ((n + 6) % 256) + 1, 'B', 'S', CONCAT('C', RIGHT(CONCAT('0000', ((n - 1) % 256) + 1), 4)),
       DATEADD(day, n - 1, CONVERT(datetime2(3), '2024-01-01T09:00:00')), DATEADD(day, 30, CONVERT(smalldatetime, DATEADD(day, n - 1, CONVERT(datetime, '2024-01-01T09:00:00')))),
       CASE n % 4 WHEN 0 THEN 'EUR' WHEN 1 THEN 'USD' WHEN 2 THEN 'GBP' ELSE NULL END,
       CONVERT(numeric(19,4), 100 + (n % 50) * 3), CASE WHEN n % 3 = 0 THEN CONVERT(decimal(19,4), 5) ELSE CONVERT(decimal(19,4), 0) END,
       CONVERT(decimal(19,4), 20 + (n % 7)), N'fixture', CONVERT(uniqueidentifier, CONCAT('00000000-0000-0000-0000-', RIGHT(CONCAT('000000000000', n), 12)))
FROM (SELECT TOP (1024) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.all_objects AS a CROSS JOIN sys.all_objects AS b) AS numbers;
SET IDENTITY_INSERT sales.Invoices OFF;

SET IDENTITY_INSERT sales.InvoiceLines ON;
INSERT sales.InvoiceLines (InvoiceLineId, InvoiceId, LineNumber, ProductId, Quantity, UnitPrice, DiscountPercent)
SELECT ((i.InvoiceId - 1) * 4) + l.LineNumber, i.InvoiceId, l.LineNumber, ((i.InvoiceId + l.LineNumber - 2) % 128) + 1,
       l.LineNumber, CONVERT(decimal(19,4), 10 + ((i.InvoiceId + l.LineNumber) % 40)), CASE WHEN (i.InvoiceId + l.LineNumber) % 4 = 0 THEN 0.10 ELSE 0 END
FROM sales.Invoices AS i CROSS JOIN (VALUES (1), (2), (3), (4)) AS l(LineNumber);
SET IDENTITY_INSERT sales.InvoiceLines OFF;

SET IDENTITY_INSERT sales.Payments ON;
INSERT sales.Payments (PaymentId, InvoiceId, PaidAt, AmountPaid, PaymentReference)
SELECT n, n * 2, TODATETIMEOFFSET(DATEADD(day, n * 2, CONVERT(datetime2(0), '2024-01-01T12:00:00')), '+00:00'), CONVERT(money, 50 + n), CASE WHEN n % 8 = 0 THEN NULL ELSE CONCAT('PAY-', n) END
FROM (SELECT TOP (512) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.all_objects AS a CROSS JOIN sys.all_objects AS b) AS numbers;
SET IDENTITY_INSERT sales.Payments OFF;

INSERT sales.InvoiceStatuses (InvoiceId, StatusCode, StatusChangedAt)
SELECT InvoiceId, CASE WHEN InvoiceId % 5 = 0 THEN 'Paid' WHEN InvoiceId % 7 = 0 THEN 'Overdue' WHEN InvoiceId % 2 = 0 THEN 'Open' ELSE 'Draft' END,
       DATEADD(hour, InvoiceId, CONVERT(datetime2(3), '2024-01-01T00:00:00'))
FROM sales.Invoices;

INSERT operations.TypeCoverage (TypeCoverageId, Flag, TinyValue, SmallValue, IntValue, BigValue, DecimalValue, NumericValue, MoneyValue, RealValue, FloatValue, CharValue, VarCharValue, NCharValue, NVarCharValue, MaxText, JsonText, GuidValue, DateValue, TimeValue, DateTimeValue, SmallDateTimeValue, DateTime2Value, DateTimeOffsetValue, BinaryValue, VarBinaryValue, XmlValue, VariantValue, HierarchyValue)
SELECT n, CASE WHEN n % 2 = 0 THEN 1 ELSE 0 END, n, n * 10, n * 100, n * 1000, n + 0.1250, n + 0.2500, n + 0.5, n / 10.0, n / 5.0,
       RIGHT(CONCAT('000', n), 4), CONCAT('varchar-', n), RIGHT(CONCAT(N'000', n), 4), CONCAT(N'nvarchar-', n), CASE WHEN n % 3 = 0 THEN NULL ELSE CONCAT('text-', n) END,
       CASE WHEN n % 4 = 0 THEN NULL ELSE CONCAT(N'{"value":', n, N'}') END, CONVERT(uniqueidentifier, CONCAT('00000000-0000-0000-0000-', RIGHT(CONCAT('000000000000', 10000 + n), 12))),
       DATEADD(day, n, CONVERT(date, '2024-01-01')), CONVERT(time(3), '12:34:56.789'), DATEADD(day, n, CONVERT(datetime, '2024-01-01T00:00:00')),
       DATEADD(day, n, CONVERT(smalldatetime, '2024-01-01T00:00:00')), DATEADD(day, n, CONVERT(datetime2(3), '2024-01-01T00:00:00')),
       TODATETIMEOFFSET(DATEADD(day, n, CONVERT(datetime2(0), '2024-01-01T00:00:00')), '+00:00'), CONVERT(binary(4), n), CONVERT(varbinary(16), CONCAT('value-', n)),
       CASE WHEN n % 2 = 0 THEN CONVERT(xml, CONCAT('<value>', n, '</value>')) ELSE NULL END, CONVERT(sql_variant, n), hierarchyid::Parse(CONCAT('/', n, '/'))
FROM (SELECT TOP (16) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.all_objects) AS numbers;

INSERT archive.Invoices (ArchiveInvoiceId, InvoiceNumber, ArchivedAt, ArchivedReason)
SELECT n, CONCAT('ARC-', RIGHT(CONCAT('000000', n), 6)), DATEADD(day, n, CONVERT(datetime2(3), '2023-01-01T00:00:00')), CASE WHEN n % 4 = 0 THEN NULL ELSE N'Retention test fixture' END
FROM (SELECT TOP (32) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.all_objects) AS numbers;
GO
