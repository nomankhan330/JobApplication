using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class SalesInvoiceDetail
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public int PaymentTypeId { get; set; }

    public int PaymentHeaderId { get; set; }

    public int Qty { get; set; }

    public decimal Rate { get; set; }

    public decimal Amount { get; set; }

    public decimal VatPercent { get; set; }

    public decimal VatAmount { get; set; }

    public decimal TotalAmount { get; set; }
}
