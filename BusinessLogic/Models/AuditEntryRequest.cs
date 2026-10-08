using System;
using System.Collections.Generic;

namespace BusinessLogic.Models
{
    public class AuditEntryRequest
    {
        /// <summary>Business area, e.g. "Sales Invoice", "Job", "Payment Received".</summary>
        public string Module { get; set; } = string.Empty;

        /// <summary>Create / Update / Delete / Cancel / Approve / Reject.</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>Table or entity name, e.g. "SalesInvoice".</summary>
        public string EntityName { get; set; } = string.Empty;

        /// <summary>Business key shown in the log, e.g. "INV-2026-0004".</summary>
        public string? EntityId { get; set; }

        public string? Description { get; set; }

        /// <summary>Snapshot taken before the change.</summary>
        public IDictionary<string, object?>? OldValues { get; set; }

        /// <summary>Snapshot taken after the change.</summary>
        public IDictionary<string, object?>? NewValues { get; set; }

        /// <summary>Page / route the action was triggered from.</summary>
        public string? PageName { get; set; }

        /// <summary>
        /// Overrides the session user. Needed for Login / Logout, where the session is
        /// empty (login) or already cleared (logout) at the time of the write.
        /// </summary>
        public int? UserIdOverride { get; set; }

        public string? UserNameOverride { get; set; }
    }
}
