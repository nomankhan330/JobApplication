using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class City
{
    public int Id { get; set; }

    public int? CountryId { get; set; }

    public string? Name { get; set; }

    public bool? IsActive { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? CreatedOn { get; set; }

    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }
}
