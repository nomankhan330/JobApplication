using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class ShipmentMode
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public bool? IsActive { get; set; }
}
