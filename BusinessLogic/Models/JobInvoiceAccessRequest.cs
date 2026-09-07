using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class JobInvoiceAccessRequest
{
    public int Id { get; set; }

    public int? ReferenceId { get; set; }

    public int JobImportMasterId { get; set; }

    public string? JobNumber { get; set; }

    public int RequestedBy { get; set; }

    public DateTime RequestedOn { get; set; }

    public bool IsApproved { get; set; }

    public int? ApprovedBy { get; set; }

    public DateTime? ApprovedOn { get; set; }

    public string? Remarks { get; set; }
}
