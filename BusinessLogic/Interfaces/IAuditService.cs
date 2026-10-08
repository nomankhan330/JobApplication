using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BusinessLogic.Models;

namespace BusinessLogic.Interfaces
{
    public interface IAuditService
    {
        /// <summary>
        /// Captures the scalar (non-navigation) properties of an object so they can be
        /// compared later. Call this BEFORE mutating the entity.
        /// </summary>
        IDictionary<string, object?> Snapshot(object? entity);

        /// <summary>
        /// Resolves customer / service-provider / job ids to readable names.
        /// Ids of 0 or null are skipped, so callers can pass raw ids straight through.
        /// </summary>
        Task<AuditNameMap> ResolveNamesAsync(int? customerId = null, int? jobId = null);

        /// <summary>
        /// Persists one audit entry. Never throws - a failing audit write must not
        /// break the business operation that triggered it.
        /// </summary>
        Task RecordAsync(AuditEntryRequest request);

        Task<byte[]> ExportExcel(IList<QueryFilters> filters);
        Task<byte[]> ExportPdf(IList<QueryFilters> filters);
    }
}
