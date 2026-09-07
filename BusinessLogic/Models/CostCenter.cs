using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class CostCenter
{
    public int Id { get; set; }

    public string? CostCenterCode { get; set; }

    public string CostCenterName { get; set; } = null!;

    public string? Address { get; set; }

    public string? ContactPerson { get; set; }

    public string? ContactNumber { get; set; }

    public string? Cnic { get; set; }

    public string? Photo { get; set; }

    public string? Notes { get; set; }

    public string? AdditionalFields { get; set; }

    public decimal? MyPercentage { get; set; }

    public bool? IsActive { get; set; }

    public int? ReferenceId { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? CreatedOn { get; set; }

    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }
}
