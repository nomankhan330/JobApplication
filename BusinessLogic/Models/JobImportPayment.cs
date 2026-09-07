using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class JobImportPayment
{
    public int Id { get; set; }

    public int JobImportMasterId { get; set; }

    public int? PaymentType { get; set; }

    public int? PaymentHeaderId { get; set; }

    public string? InvoiceNo { get; set; }

    public DateTime? InvoiceDate { get; set; }

    public decimal? Amount { get; set; }

    public string? PaymentReference { get; set; }

    public string? PaidBy { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? CreatedOn { get; set; }

    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }
}
