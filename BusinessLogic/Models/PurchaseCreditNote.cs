using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class PurchaseCreditNote
{
    public int CreditNoteId { get; set; }

    public string CreditNoteNo { get; set; } = null!;

    public int InvoiceId { get; set; }

    public string InvoiceNo { get; set; } = null!;

    public DateTime InvoiceDate { get; set; }

    public int ServiceProviderId { get; set; }

    public DateTime CreditNoteDate { get; set; }

    public decimal NetAmount { get; set; }

    public decimal VatAmount { get; set; }

    public decimal GrandTotal { get; set; }

    public string? Remarks { get; set; }

    public bool IsActive { get; set; }

    public int ReferenceId { get; set; }

    public int CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; }

    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }
}
