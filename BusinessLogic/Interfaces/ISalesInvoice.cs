using BusinessLogic.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Interfaces
{
    public interface ISalesInvoice : IDisposable
    {
        public Task<dynamic> Save(SalesInvoiceVM model, int LoginId);
        public Task<dynamic> GetInvoices(List<QueryFilters> filters);
        public Task<byte[]> ExportExcel(IList<QueryFilters> filters);
        public Task<byte[]> ExportPdf(IList<QueryFilters> filters);
        public Task<dynamic> GetInvoiceById(int InvoiceId, int? ReferenceId = null);
        public Task<dynamic> RegenerateInvoices(DateTime? FromDate, DateTime? ToDate);
        public Task<dynamic> EditSalesInvoice(int InvoiceId);
        public Task<dynamic> SavePaymentReceived(PaymentReceived model);
        public Task<dynamic> GetPaymentHistory(int InvoiceId);
        public Task<dynamic> GetSalesReport(IList<QueryFilters> filters);
        public Task<dynamic> GetCustomerStatement(IList<QueryFilters> filters);
        public Task<dynamic> GetAgingReport(IList<QueryFilters> filters);
        public Task<dynamic> GetGovernmentTaxReport(IList<QueryFilters> filters);
        public Task<byte[]> DownloadGovernmentTaxPdf(DateTime fromDate, DateTime toDate);
        public Task<byte[]> DownloadGovernmentTaxExcel(DateTime fromDate, DateTime toDate);
        public Task<bool> IsInvoiceEditable(int InvoiceId);
        public Task<dynamic> GetNextSalesInvoiceNumber();
        public Task<dynamic> GetNextCreditNoteNumber();
        public Task<dynamic> CancelInvoice(int invoiceId);
        public Task<dynamic> GetCreditNoteSummary(int invoiceId);
    }
}
