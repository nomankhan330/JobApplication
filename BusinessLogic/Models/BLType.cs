using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class Bltype
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public bool? IsActive { get; set; }
}
