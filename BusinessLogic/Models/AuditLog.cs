using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class AuditLog
{
    public long Id { get; set; }

    public DateTime AuditDate { get; set; }

    public int? UserId { get; set; }

    public string? UserName { get; set; }

    public int? UserType { get; set; }

    public int? ReferenceId { get; set; }

    public string? Module { get; set; }

    public string? Action { get; set; }

    public string? EntityName { get; set; }

    public string? EntityId { get; set; }

    public string? Description { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? PageName { get; set; }
}
