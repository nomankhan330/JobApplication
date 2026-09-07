using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class Log
{
    public Guid LogId { get; set; }

    public DateTime? LogDate { get; set; }

    public string? PageName { get; set; }

    public string? FunctionName { get; set; }

    public string? ErrorType { get; set; }

    public string? Error { get; set; }
}
