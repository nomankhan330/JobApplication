-- ============================================================
-- MISSING INDEXES FOR SCALABILITY (millions of rows)
-- Run AFTER seeding test data. Safe to run on existing DB too.
-- Each index is guarded by IF NOT EXISTS.
-- ============================================================
SET NOCOUNT ON;

-- ------------------------------------------------------------
-- JobImportMaster
-- Used by: GetJobs (ReferenceId filter, UserId/CreatedBy,
--          BLNumber search, FromDate/ToDate, CostCenter filter)
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobImportMaster_ReferenceId')
BEGIN
    CREATE INDEX IX_JobImportMaster_ReferenceId ON JobImportMaster (ReferenceId);
    PRINT 'Created IX_JobImportMaster_ReferenceId';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobImportMaster_CreatedBy')
BEGIN
    CREATE INDEX IX_JobImportMaster_CreatedBy ON JobImportMaster (CreatedBy);
    PRINT 'Created IX_JobImportMaster_CreatedBy';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobImportMaster_CreatedOn')
BEGIN
    CREATE INDEX IX_JobImportMaster_CreatedOn ON JobImportMaster (CreatedOn);
    PRINT 'Created IX_JobImportMaster_CreatedOn';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobImportMaster_BlNo')
BEGIN
    CREATE INDEX IX_JobImportMaster_BlNo ON JobImportMaster (BlNo);
    PRINT 'Created IX_JobImportMaster_BlNo';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobImportMaster_ShipmentType')
BEGIN
    CREATE INDEX IX_JobImportMaster_ShipmentType ON JobImportMaster (ShipmentType);
    PRINT 'Created IX_JobImportMaster_ShipmentType';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobImportMaster_Ref_CreatedBy')
BEGIN
    CREATE INDEX IX_JobImportMaster_Ref_CreatedBy
        ON JobImportMaster (ReferenceId, CreatedBy, CreatedOn);
    PRINT 'Created IX_JobImportMaster_Ref_CreatedBy';
END;

-- ------------------------------------------------------------
-- JobImportPayment
-- Used by: GetJobById / TotalJobAmount (Sum by JobImportMasterId),
--          payment list queries
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobImportPayment_Master_Invoice')
BEGIN
    CREATE INDEX IX_JobImportPayment_Master_Invoice
        ON JobImportPayment (JobImportMasterId, InvoiceNo);
    PRINT 'Created IX_JobImportPayment_Master_Invoice';
END;

-- ------------------------------------------------------------
-- SalesInvoice
-- Used by: GetInvoices (ReferenceId, search, dates), dashboard sums
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SalesInvoice_ReferenceId')
BEGIN
    CREATE INDEX IX_SalesInvoice_ReferenceId ON SalesInvoice (ReferenceId);
    PRINT 'Created IX_SalesInvoice_ReferenceId';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SalesInvoice_CreatedOn')
BEGIN
    CREATE INDEX IX_SalesInvoice_CreatedOn ON SalesInvoice (CreatedOn);
    PRINT 'Created IX_SalesInvoice_CreatedOn';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SalesInvoice_InvoiceDate')
BEGIN
    CREATE INDEX IX_SalesInvoice_InvoiceDate ON SalesInvoice (InvoiceDate);
    PRINT 'Created IX_SalesInvoice_InvoiceDate';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SalesInvoice_Ref_Date')
BEGIN
    CREATE INDEX IX_SalesInvoice_Ref_Date
        ON SalesInvoice (ReferenceId, InvoiceDate, CreatedOn);
    PRINT 'Created IX_SalesInvoice_Ref_Date';
END;

-- ------------------------------------------------------------
-- PurchaseInvoice
-- Used by: GetInvoices (ReferenceId, search, dates), dashboard sums
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseInvoice_ReferenceId')
BEGIN
    CREATE INDEX IX_PurchaseInvoice_ReferenceId ON PurchaseInvoice (ReferenceId);
    PRINT 'Created IX_PurchaseInvoice_ReferenceId';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseInvoice_CreatedOn')
BEGIN
    CREATE INDEX IX_PurchaseInvoice_CreatedOn ON PurchaseInvoice (CreatedOn);
    PRINT 'Created IX_PurchaseInvoice_CreatedOn';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseInvoice_InvoiceDate')
BEGIN
    CREATE INDEX IX_PurchaseInvoice_InvoiceDate ON PurchaseInvoice (InvoiceDate);
    PRINT 'Created IX_PurchaseInvoice_InvoiceDate';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseInvoice_Ref_Date')
BEGIN
    CREATE INDEX IX_PurchaseInvoice_Ref_Date
        ON PurchaseInvoice (ReferenceId, InvoiceDate, CreatedOn);
    PRINT 'Created IX_PurchaseInvoice_Ref_Date';
END;

-- ------------------------------------------------------------
-- User
-- Used by: login queries, AssignedUser lookups, dashboard
--          Admin/Authorize/UnAuthorize counts (joins on UserType)
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_User_ReferenceId')
BEGIN
    CREATE INDEX IX_User_ReferenceId ON Users (ReferenceId);
    PRINT 'Created IX_User_ReferenceId';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_User_LoginType')
BEGIN
    CREATE INDEX IX_User_LoginType ON Users (LoginType);
    PRINT 'Created IX_User_LoginType';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_User_UserType')
BEGIN
    CREATE INDEX IX_User_UserType ON Users (UserType);
    PRINT 'Created IX_User_UserType';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_User_Email')
BEGIN
    CREATE INDEX IX_User_Email ON Users (Email);
    PRINT 'Created IX_User_Email';
END;

-- ------------------------------------------------------------
-- Customer
-- Used by: dashboard Customers/Service Providers counts, dropdowns
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Customer_ReferenceId_TypeId')
BEGIN
    CREATE INDEX IX_Customer_ReferenceId_TypeId ON Customer (ReferenceId, TypeId);
    PRINT 'Created IX_Customer_ReferenceId_TypeId';
END;

-- ------------------------------------------------------------
-- UserCostCenters
-- Used by: allowedCostCenters.Contains filter in GetJobs,
--          multi cost-center filtering
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserCostCenters_UserId_CostCenterId')
BEGIN
    CREATE INDEX IX_UserCostCenters_UserId_CostCenterId
        ON UserCostCenters (UserId, CostCenterId);
    PRINT 'Created IX_UserCostCenters_UserId_CostCenterId';
END;

PRINT '=== INDEX SCRIPT COMPLETE ===';