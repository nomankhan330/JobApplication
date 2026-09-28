IF OBJECT_ID('dbo.sp_GetPurchaseReport', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetPurchaseReport;
GO

CREATE PROCEDURE [dbo].[sp_GetPurchaseReport]
(
    @ReferenceId INT = NULL,
    @CostCenterId INT = NULL,
    @FromDate DATE = NULL,
    @ToDate DATE = NULL,
    @Status NVARCHAR(50) = NULL,
    @OrderColumn NVARCHAR(50) = 'InvoiceId',
    @OrderDir NVARCHAR(4) = 'desc'
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.InvoiceId,
        p.InvoiceDate,
        p.InvoiceNo,
        ISNULL(p.GrandTotal, 0) AS InvoiceAmount,

        dbo.fn_GetPaymentStatusName(p.Status) Status,

        j.Id AS JobId,
        j.JobDate,
        j.JobNumber AS JobNo,
        j.BlNo AS BLNumber,
        j.ShipmentType,

        CASE
            WHEN j.ShipmentType = 1 THEN 'Import'
            ELSE 'Export'
        END AS ShipmentTypeName,

        sm.Name AS ModeOfShipment,
        ISNULL(sp.CustomerName, p.ServiceProviderName) AS ServiceProviderName,

        c.Id AS CostCenterId,
        c.CostCenterName AS CostCenter,
        ISNULL(c.MyPercentage, 0) AS Percentage,

        calc.TotalCost,
        calc.VATAmount,

        gp.GrossProfit,

        CASE
            WHEN gp.GrossProfit < 0 THEN ABS(gp.GrossProfit)
            ELSE 0
        END AS LossAmount,

        CASE
            WHEN gp.GrossProfit > 0
            THEN (gp.GrossProfit * ISNULL(c.MyPercentage, 0)) / 100
            ELSE 0
        END AS Profit,

        CASE
            WHEN gp.GrossProfit > 0
            THEN gp.GrossProfit - ((gp.GrossProfit * ISNULL(c.MyPercentage, 0)) / 100)
            ELSE 0
        END AS Commission,

        pv.PVNumber AS Remarks

    FROM PurchaseInvoice p

    LEFT JOIN JobImportMaster j
        ON p.JobId = j.Id

    LEFT JOIN CostCenters c
        ON j.CostCenterId = c.Id

    LEFT JOIN Customer sp
        ON p.ServiceProviderId = sp.Id

    LEFT JOIN ShipmentMode sm
        ON j.ShipmentModeId = sm.Id

    OUTER APPLY (
        SELECT TOP 1
            v.PVNumber
        FROM PurchaseVoucher v
        WHERE v.InvoiceId = p.InvoiceId
        ORDER BY v.PaymentDate DESC
    ) pv

    CROSS APPLY (
        SELECT
            ISNULL((
                SELECT SUM(jp.Amount)
                FROM JobImportPayment jp
                WHERE jp.JobImportMasterId = j.Id
            ), 0) AS TotalCost,

            ISNULL((
                SELECT SUM(pd.VatAmount)
                FROM PurchaseInvoiceDetail pd
                WHERE pd.InvoiceId = p.InvoiceId
            ), 0) AS VATAmount
    ) calc

    CROSS APPLY (
        SELECT (ISNULL(p.GrandTotal, 0) - calc.TotalCost - calc.VATAmount) AS GrossProfit
    ) gp

    WHERE (@ReferenceId IS NULL OR p.ReferenceId = @ReferenceId)
        AND (@CostCenterId IS NULL OR j.CostCenterId = @CostCenterId)
        AND (@FromDate IS NULL OR p.InvoiceDate >= @FromDate)
        AND (@ToDate IS NULL OR p.InvoiceDate <= @ToDate)
        AND (@Status IS NULL OR p.Status = @Status)

    ORDER BY
        CASE WHEN @OrderColumn = 'InvoiceNo' AND @OrderDir = 'asc' THEN p.InvoiceNo END ASC,
        CASE WHEN @OrderColumn = 'InvoiceNo' AND @OrderDir = 'desc' THEN p.InvoiceNo END DESC,

        CASE WHEN @OrderColumn = 'InvoiceDate' AND @OrderDir = 'asc' THEN p.InvoiceDate END ASC,
        CASE WHEN @OrderColumn = 'InvoiceDate' AND @OrderDir = 'desc' THEN p.InvoiceDate END DESC,

        CASE WHEN @OrderColumn = 'ServiceProviderName' AND @OrderDir = 'asc' THEN ISNULL(sp.CustomerName, p.ServiceProviderName) END ASC,
        CASE WHEN @OrderColumn = 'ServiceProviderName' AND @OrderDir = 'desc' THEN ISNULL(sp.CustomerName, p.ServiceProviderName) END DESC,

        CASE WHEN @OrderColumn = 'CostCenter' AND @OrderDir = 'asc' THEN c.CostCenterName END ASC,
        CASE WHEN @OrderColumn = 'CostCenter' AND @OrderDir = 'desc' THEN c.CostCenterName END DESC,

        CASE WHEN @OrderColumn = 'JobNo' AND @OrderDir = 'asc' THEN j.JobNumber END ASC,
        CASE WHEN @OrderColumn = 'JobNo' AND @OrderDir = 'desc' THEN j.JobNumber END DESC,

        p.InvoiceId DESC;
END
GO