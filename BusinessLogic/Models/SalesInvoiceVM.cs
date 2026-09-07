using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class SalesInvoiceVM
{
    public int InvoiceId { get; set; }

    public string InvoiceNo { get; set; } = null!;

    public int CustomerId { get; set; }

    public int JobId { get; set; }

    public DateTime InvoiceDate { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal Advance { get; set; }

    public decimal Discount { get; set; }

    public decimal NetAmount { get; set; }

    public decimal VatAmount { get; set; }

    public decimal GrandTotal { get; set; }

    public int? Status { get; set; } = 0!;

    public int ReferenceId { get; set; }

    public int CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; }

    public int ModifiedBy { get; set; }

    public DateTime ModifiedOn { get; set; }

    public bool IsCancelled { get; set; }

    public int? CancelledBy { get; set; }

    public DateTime? CancelledOn { get; set; }

    public string? CancelRemarks { get; set; }

    public List<SalesInvoiceDetail> Items { get; set; } = new List<SalesInvoiceDetail>();
}
