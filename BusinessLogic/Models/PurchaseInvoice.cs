using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class PurchaseInvoice
{
    public int InvoiceId { get; set; }

    public string InvoiceNo { get; set; } = null!;

    public int? ServiceProviderId { get; set; }

    public string? ServiceProviderName { get; set; }

    public string? ServiceProviderAddress { get; set; }

    public string? ServiceProviderRegion { get; set; }

    public string? ServiceProviderCountry { get; set; }

    public string? ServiceProviderVatId { get; set; }

    public int JobId { get; set; }

    public DateTime InvoiceDate { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal Advance { get; set; }

    public decimal Discount { get; set; }

    public decimal NetAmount { get; set; }

    public decimal VatAmount { get; set; }

    public decimal GrandTotal { get; set; }

    public decimal? PaidAmount { get; set; }

    public decimal? BalanceAmount { get; set; }

    public int Status { get; set; }

    public int? ReferenceId { get; set; }

    public int CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; }

    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public bool? IsCancelled { get; set; }

    public int? CancelledBy { get; set; }

    public DateTime? CancelledOn { get; set; }

    public string? CancelRemarks { get; set; }
}
