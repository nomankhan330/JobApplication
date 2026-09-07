using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class ShipmentStatus
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public bool? IsActive { get; set; }
}
