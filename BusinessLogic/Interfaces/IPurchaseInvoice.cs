using BusinessLogic.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Interfaces
{
    public interface IPurchaseInvoice : IDisposable
    {
        public Task<dynamic> Save(PurchaseInvoiceVM model, int LoginId);
        public Task<dynamic> GetInvoices(List<QueryFilters> filters);
        public Task<dynamic> GetInvoiceById(int InvoiceId, int ServiceProviderId);
        public Task<dynamic> SavePurchaseVoucher(PurchaseVoucher model);
        public Task<dynamic> EditPurchaseInvoice(int invoiceId);
        public Task<dynamic> GetSalesReport(IList<QueryFilters> filters);
        public Task<dynamic> GetServiceProviderStatement(IList<QueryFilters> filters);
        public Task<bool> IsInvoiceEditable(int InvoiceId);
        public Task<dynamic> GetNextCreditNoteNumber();
        public Task<dynamic> GetNextPurchaseInvoiceNumber();
        public Task<dynamic> CancelInvoice(int invoiceId);
        public Task<dynamic> GetCreditNoteSummary(int invoiceId);
    }
}