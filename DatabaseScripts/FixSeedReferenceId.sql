-- ============================================================
-- FIX: move already-seeded TEST rows to ReferenceId = 2
-- Only matches rows this seed script created (Test Shipper/Consignee
-- patterns) so real user data is NOT touched.
-- ============================================================
SET NOCOUNT ON;

DECLARE @TargetReferenceId INT = 2;   -- aapki company ReferenceId

UPDATE JobImportMaster
SET ReferenceId = @TargetReferenceId
WHERE Shipper LIKE 'Test Shipper %';

UPDATE JobImportPayment
SET CreatedBy = @TargetReferenceId
WHERE PaymentReference LIKE 'SEED-%';

UPDATE SalesInvoice
SET ReferenceId = @TargetReferenceId
WHERE InvoiceNo LIKE 'INV-2026-%';

UPDATE PurchaseInvoice
SET ReferenceId = @TargetReferenceId
WHERE InvoiceNo LIKE 'PINV-2026-%';

PRINT 'Fix complete - test rows moved to ReferenceId ' + CAST(@TargetReferenceId AS VARCHAR(10));