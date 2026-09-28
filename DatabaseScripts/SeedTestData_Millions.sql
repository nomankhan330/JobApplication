-- ============================================================
-- STRESS TEST SEED SCRIPT
-- Generates millions of rows: JobImportMaster, JobImportPayment,
-- SalesInvoice + Detail, PurchaseInvoice + Detail.
--
-- HOW TO USE:
--   1. Open SQL Server Management Studio
--   2. Select the JobApplication database
--   3. Set @TotalJobs / @TotalSales / @TotalPurchase as needed
--   4. Run
--
-- NOTES:
--   * JobImportMaster.Id is explicit (app uses MAX(Id)+1, so this
--     script starts from MAX(Id) to preserve existing data).
--   * Invoice numbers generated here do not advance the app's
--     SQL SEQUENCE objects, so no collisions with new invoices.
--   * Details are uniformly distributed: @DetailsPerInv rows per
--     invoice (no orphan/gap rows).
-- ============================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE
    @TotalJobs      INT = 200000,   -- how many JobImportMaster rows
    @TotalSales     INT = 150000,   -- how many SalesInvoice rows
    @TotalPurchase  INT = 150000,   -- how many PurchaseInvoice rows
    @DetailsPerInv  INT = 2,        -- detail rows per invoice
    @ReferenceId    INT = 2,        -- company id (Users.ReferenceId) that owns the data
    @CreatedBy      INT = 2,        -- user id (Users.Id) creating the data
    @StartJobId     INT,
    @NumCount       INT = 0,
    @NewJobCount    INT = 0,
    @NewSalesCount  INT = 0,
    @NewPurchaseCount INT = 0;

-- ------------------------------------------------------------
-- 1. NUMBERS (TALLY) TABLE 1..1,000,000
-- ------------------------------------------------------------
IF OBJECT_ID('tempdb..#Numbers') IS NOT NULL DROP TABLE #Numbers;
WITH E1(n) AS (SELECT 1 UNION ALL SELECT 1 UNION ALL SELECT 1 UNION ALL SELECT 1 UNION ALL SELECT 1 UNION ALL SELECT 1 UNION ALL SELECT 1 UNION ALL SELECT 1 UNION ALL SELECT 1 UNION ALL SELECT 1),      -- 10
     E2(n) AS (SELECT 1 FROM E1 a CROSS JOIN E1 b),                                                                                                                             -- 100
     E3(n) AS (SELECT 1 FROM E2 a CROSS JOIN E2 b),                                                                                                                             -- 10,000
     E4(n) AS (SELECT 1 FROM E3 a CROSS JOIN E3 b)                                                                                                                              -- 100,000,000
SELECT TOP (1000000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Num
INTO #Numbers
FROM E4;

SELECT @NumCount = @@ROWCOUNT;
PRINT 'Numbers table ready: ' + CAST(@NumCount AS VARCHAR(20));

-- ------------------------------------------------------------
-- 2. STARTING JOB ID (preserve existing data)
-- ------------------------------------------------------------
SELECT @StartJobId = ISNULL(MAX(Id), 0) FROM JobImportMaster;
PRINT 'StartJobId = ' + CAST(@StartJobId AS VARCHAR(20));

-- ------------------------------------------------------------
-- 3. LOOKUP TABLES WITH CYCLE KEY (Num % Count + 1)
-- ------------------------------------------------------------
IF OBJECT_ID('tempdb..#LookupCostCenter') IS NOT NULL DROP TABLE #LookupCostCenter;
SELECT Id, COUNT(*) OVER () AS Cnt,
       ROW_NUMBER() OVER (ORDER BY Id) AS Rn
INTO #LookupCostCenter
FROM CostCenter WHERE IsActive = 1;

IF OBJECT_ID('tempdb..#LookupCustomer') IS NOT NULL DROP TABLE #LookupCustomer;
SELECT Id, COUNT(*) OVER () AS Cnt,
       ROW_NUMBER() OVER (ORDER BY Id) AS Rn
INTO #LookupCustomer
FROM Customer WHERE IsActive = 1;

IF OBJECT_ID('tempdb..#LookupPol') IS NOT NULL DROP TABLE #LookupPol;
SELECT Id, COUNT(*) OVER () AS Cnt,
       ROW_NUMBER() OVER (ORDER BY Id) AS Rn
INTO #LookupPol
FROM Pol;

IF OBJECT_ID('tempdb..#LookupPod') IS NOT NULL DROP TABLE #LookupPod;
SELECT Id, COUNT(*) OVER () AS Cnt,
       ROW_NUMBER() OVER (ORDER BY Id) AS Rn
INTO #LookupPod
FROM Pod;

IF OBJECT_ID('tempdb..#LookupProvider') IS NOT NULL DROP TABLE #LookupProvider;
SELECT Id, CustomerName, COUNT(*) OVER () AS Cnt,
       ROW_NUMBER() OVER (ORDER BY Id) AS Rn
INTO #LookupProvider
FROM Customer WHERE TypeId = 2 AND IsActive = 1;

-- ------------------------------------------------------------
-- 4. SEED JobImportMaster
-- ------------------------------------------------------------
INSERT INTO JobImportMaster
    (Id, ShipmentType, CostCenterId, JobNumber, JobDate, ShipmentModeId,
     JobTypeId, CustomerId, Shipper, Consignee, Carrier, PolId, PodId,
     ContainerQuantity, ContainerNo, ContainerTypeId, DeliveryCityId,
     ShipmentStatusId, ReferenceId, CreatedBy, CreatedOn)
SELECT
    @StartJobId + N.Num,
    CASE WHEN N.Num % 2 = 1 THEN 1 ELSE 2 END,                          -- Import/Export alternating
    cc.Id,
    'JBN-' + RIGHT('000000' + CAST(@StartJobId + N.Num AS VARCHAR(12)), 6),
    DATEADD(DAY, (N.Num % 1200), '2023-01-01'),                         -- spread over ~3 years
    1,                                                                  -- ShipmentModeId (existing)
    1,                                                                  -- JobTypeId (existing)
    cu.Id,
    'Test Shipper ' + CAST(@StartJobId + N.Num AS VARCHAR(12)),
    'Test Consignee ' + CAST(@StartJobId + N.Num AS VARCHAR(12)),
    'Test Carrier',
    pol.Id,
    pod.Id,
    '5',
    'TCLU' + RIGHT('0000000' + CAST(@StartJobId + N.Num AS VARCHAR(12)), 7),
    1,                                                                  -- ContainerTypeId (existing)
    1,                                                                  -- DeliveryCityId (existing)
    1,                                                                  -- ShipmentStatusId (existing)
    @ReferenceId,                                                        -- ReferenceId (company that owns data)
    @CreatedBy,                                                          -- CreatedBy
    DATEADD(SECOND, N.Num % 10000000, '2023-01-01')
FROM #Numbers N
JOIN #LookupCostCenter   cc ON cc.Rn  = (N.Num % cc.Cnt) + 1
JOIN #LookupCustomer     cu ON cu.Rn  = (N.Num % cu.Cnt) + 1
JOIN #LookupPol          pol ON pol.Rn = (N.Num % pol.Cnt) + 1
JOIN #LookupPod          pod ON pod.Rn = (N.Num % pod.Cnt) + 1
WHERE N.Num <= @TotalJobs;

PRINT 'JobImportMaster seeded: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
SELECT @NewJobCount = COUNT(*) FROM JobImportMaster;
PRINT 'New JobImportMaster count: ' + CAST(@NewJobCount AS VARCHAR(20));

-- ------------------------------------------------------------
-- 5. SEED JobImportPayment (one payment per job)
-- ------------------------------------------------------------
INSERT INTO JobImportPayment
    (JobImportMasterId, PaymentType, PaymentHeaderId, InvoiceNo,
     InvoiceDate, Amount, PaymentReference, PaidBy, CreatedBy, CreatedOn)
SELECT
    @StartJobId + N.Num,
    1,
    1,
    'PAY-' + CAST(@StartJobId + N.Num AS VARCHAR(12)),
    DATEADD(DAY, (N.Num % 1200), '2023-01-01'),
    5000.00 + (N.Num % 100) * 100,
    'SEED-' + CAST(@StartJobId + N.Num AS VARCHAR(12)),
    'System',
    @CreatedBy,
    DATEADD(SECOND, N.Num % 10000000, '2023-01-01')
FROM #Numbers N
WHERE N.Num <= @TotalJobs
  AND NOT EXISTS (SELECT 1 FROM JobImportPayment JIP WHERE JIP.JobImportMasterId = @StartJobId + N.Num);

PRINT 'JobImportPayment seeded: ' + CAST(@@ROWCOUNT AS VARCHAR(20));

-- ------------------------------------------------------------
-- 6. SEED SalesInvoice
-- ------------------------------------------------------------
INSERT INTO SalesInvoice
    (InvoiceNo, CustomerId, JobId, InvoiceDate, TotalAmount, Advance,
     Discount, NetAmount, VatAmount, GrandTotal, PaidAmount, BalanceAmount,
     Status, ReferenceId, CreatedBy, CreatedOn, IsCancelled)
SELECT
    'INV-2026-' + RIGHT('0000' + CAST(N.Num AS VARCHAR(12)), 7),
    cu.Id,
    @StartJobId + (N.Num % @TotalJobs) + 1,
    DATEADD(DAY, (N.Num % 1200), '2023-01-01'),
    10000.00 + (N.Num % 100) * 100,
    0, 0,
    10000.00 + (N.Num % 100) * 100,
    500.00 + (N.Num % 100) * 5,
    10500.00 + (N.Num % 100) * 105,
    NULL, NULL,
    1,                                                                 -- Status
    @ReferenceId,
    @CreatedBy,
    DATEADD(SECOND, N.Num % 10000000, '2023-01-01'),
    0
FROM #Numbers N
JOIN #LookupCustomer cu ON cu.Rn = (N.Num % cu.Cnt) + 1
WHERE N.Num <= @TotalSales;

PRINT 'SalesInvoice seeded: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
SELECT @NewSalesCount = COUNT(*) FROM SalesInvoice;
PRINT 'New SalesInvoice count: ' + CAST(@NewSalesCount AS VARCHAR(20));

-- ------------------------------------------------------------
-- 7. SEED SalesInvoiceDetail (@DetailsPerInv rows per invoice)
-- ------------------------------------------------------------
IF OBJECT_ID('tempdb..#SalesDetailSeed') IS NOT NULL DROP TABLE #SalesDetailSeed;
-- keep only those SalesInvoice rows that this script created (InvoiceNo pattern)
SELECT InvoiceId INTO #SalesDetailSeed
FROM SalesInvoice
WHERE InvoiceNo LIKE 'INV-2026-%'
  AND CAST(REPLACE(InvoiceNo, 'INV-2026-', '') AS INT) <= @TotalSales;

INSERT INTO SalesInvoiceDetail
    (InvoiceId, PaymentTypeId, PaymentHeaderId, Qty, Rate, Amount,
     VatPercent, VatAmount, TotalAmount)
SELECT

    inv.InvoiceId,
    1, 1,
    1,
    1000.00 + (D.Num % 100) * 100,
    1000.00 + (D.Num % 100) * 100,
    5,
    50.00 + (D.Num % 100) * 5,
    1050.00 + (D.Num % 100) * 105
FROM #SalesDetailSeed inv
CROSS APPLY (SELECT TOP (@DetailsPerInv) Num FROM #Numbers) D;

PRINT 'SalesInvoiceDetail seeded: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
DROP TABLE #SalesDetailSeed;

-- ------------------------------------------------------------
-- 8. SEED PurchaseInvoice
-- ------------------------------------------------------------
INSERT INTO PurchaseInvoice
    (InvoiceNo, ServiceProviderId, ServiceProviderName, ServiceProviderAddress,
     ServiceProviderRegion, ServiceProviderCountry, ServiceProviderVatId,
     JobId, InvoiceDate, TotalAmount, Advance, Discount, NetAmount, VatAmount,
     GrandTotal, PaidAmount, BalanceAmount, Status, ReferenceId, CreatedBy,
     CreatedOn, IsCancelled)
SELECT
    'PINV-2026-' + RIGHT('0000' + CAST(N.Num AS VARCHAR(12)), 7),
    pr.Id,
    pr.CustomerName,
    'Test Address',
    'Region',
    'Country',
    'VAT-123456',
    @StartJobId + (N.Num % @TotalJobs) + 1,
    DATEADD(DAY, (N.Num % 1200), '2023-01-01'),
    8000.00 + (N.Num % 100) * 100,
    0, 0,
    8000.00 + (N.Num % 100) * 100,
    400.00 + (N.Num % 100) * 4,
    8400.00 + (N.Num % 100) * 104,
    NULL, NULL,
    1,
    @ReferenceId,
    @CreatedBy,
    DATEADD(SECOND, N.Num % 10000000, '2023-01-01'),
    0
FROM #Numbers N
JOIN #LookupProvider pr ON pr.Rn = (N.Num % pr.Cnt) + 1
WHERE N.Num <= @TotalPurchase;

PRINT 'PurchaseInvoice seeded: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
SELECT @NewPurchaseCount = COUNT(*) FROM PurchaseInvoice;
PRINT 'New PurchaseInvoice count: ' + CAST(@NewPurchaseCount AS VARCHAR(20));

-- ------------------------------------------------------------
-- 9. SEED PurchaseInvoiceDetail (@DetailsPerInv rows per invoice)
-- ------------------------------------------------------------
IF OBJECT_ID('tempdb..#PurchaseDetailSeed') IS NOT NULL DROP TABLE #PurchaseDetailSeed;
-- keep only those PurchaseInvoice rows that this script created
SELECT InvoiceId INTO #PurchaseDetailSeed
FROM PurchaseInvoice
WHERE InvoiceNo LIKE 'PINV-2026-%'
  AND CAST(REPLACE(InvoiceNo, 'PINV-2026-', '') AS INT) <= @TotalPurchase;

INSERT INTO PurchaseInvoiceDetail
    (InvoiceId, PaymentTypeId, PaymentHeaderId, Qty, Rate, Amount,
     VatPercent, VatAmount, TotalAmount)
SELECT
    inv.InvoiceId,
    1, 1,
    1,
    800.00 + (D.Num % 100) * 100,
    800.00 + (D.Num % 100) * 100,
    5,
    40.00 + (D.Num % 100) * 4,
    840.00 + (D.Num % 100) * 104
FROM #PurchaseDetailSeed inv
CROSS APPLY (SELECT TOP (@DetailsPerInv) Num FROM #Numbers) D;

PRINT 'PurchaseInvoiceDetail seeded: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
DROP TABLE #PurchaseDetailSeed;

-- ------------------------------------------------------------
-- 10. CLEANUP
-- ------------------------------------------------------------
DROP TABLE #Numbers;
DROP TABLE #LookupCostCenter;
DROP TABLE #LookupCustomer;
DROP TABLE #LookupPol;
DROP TABLE #LookupPod;
DROP TABLE #LookupProvider;

PRINT '=== SEED COMPLETE ===';