using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class PaymentReceived
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public string Rvnumber { get; set; } = null!;

    public DateTime PaymentDate { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMode { get; set; } = null!;

    public string? BlNo { get; set; }

    public bool IsActive { get; set; }

    public int CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; }

    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }
}
