using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class PaymentHeader
{
    public int Id { get; set; }

    public int? PaymentTypeId { get; set; }

    public string? Headers { get; set; }

    public string? HeadersAr { get; set; }

    public decimal? Vat { get; set; }

    public bool? IsActive { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? CreatedOn { get; set; }

    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }
}
