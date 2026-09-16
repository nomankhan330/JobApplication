using BusinessLogic.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Interfaces
{
    public interface IJobImportMaster : IDisposable
    {
        public Task<dynamic> Save(JobImportMasterVM model);
        public Task<dynamic> SaveInvoicePayments(JobImportMasterVM model, int jobImportMasterId, string jobNumber, bool hasLockEntry);
        public Task<dynamic> GetJobs(IList<QueryFilters> filters);
        public Task<dynamic> GetJobsReport(IList<QueryFilters> filters);
        public Task<dynamic> GetAllJobById(int id);
        public Task<dynamic> GetJobById(int id, int? paymentTypeId = null);
        public Task<dynamic> CheckInvoiceAccess(int jobId, int userId);
        public Task<bool> CanAccessInvoice(int jobId, int userId);
        public Task<bool> CanAccessJob(int jobId, int userId);
        public Task<dynamic> RequestInvoiceAccess(int id);
        public Task<dynamic> ApproveInvoiceAccess(int requestId);
        public Task<dynamic> GetPendingRequests();
        public Task<int> CountJobs(IList<QueryFilters> filters);
        public Task<FinancialStatisticsDto> GetFinancialStatisticsAsync(DateTime currentStart, DateTime currentEnd, DateTime prevStart, DateTime prevEnd);

        public Task<List<RecentInvoiceDto>> GetRecentInvoicesAsync(int take = 10);
        public Task<List<JobOperationsShareDto>> GetJobOperationsShareAsync(string period = "ThisMonth");
    }
}
