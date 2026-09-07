using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace JobApplication.Controllers
{
    public class InvoiceController : BaseController
    {
        private readonly ISalesInvoice _salesinvoice;
        private readonly IPurchaseInvoice _purchaseinvoice;
        private readonly ISessionHelper _session;

        public InvoiceController(ISalesInvoice salesinvoice, IPurchaseInvoice purchaseinvoice, ISessionHelper session) : base(session)
        {
            _salesinvoice = salesinvoice;
            _purchaseinvoice = purchaseinvoice;
            _session = session;
        }

        #region Sales Invoice
        public IActionResult CreateSalesInvoice()
        {
            return View();
        }

        public IActionResult SalesInvoice()
        {
            return View();
        }

        public IActionResult SalesInvoiceView(int? id)
        {
            ViewBag.Id = id;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SaveSalesInvoice([FromBody] SalesInvoiceVM model)
        {
            var result = await _salesinvoice.Save(model, _session.LoginId);

            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<IActionResult> GetSalesInvoices()
        {
            var filters = new List<QueryFilters>
            {
                new QueryFilters { fieldName = "draw", filterValue = Request.Form["draw"] },
                new QueryFilters { fieldName = "start", filterValue = Request.Form["start"] },
                new QueryFilters { fieldName = "length", filterValue = Request.Form["length"] },
                new QueryFilters { fieldName = "search", filterValue = Request.Form["search[value]"] },

                new QueryFilters { fieldName = "orderColumn", filterValue = Request.Form["order[0][column]"] },
                new QueryFilters { fieldName = "orderDir", filterValue = Request.Form["order[0][dir]"] },

                new QueryFilters { fieldName = "CustomerId", filterValue = Request.Form["customerId"] },
                new QueryFilters { fieldName = "FromDate", filterValue = Request.Form["fromDate"] },
                new QueryFilters { fieldName = "ToDate", filterValue = Request.Form["toDate"] },
                new QueryFilters { fieldName = "InvoiceNo", filterValue = Request.Form["invoiceNo"] },
                new QueryFilters { fieldName = "JobNo", filterValue = Request.Form["jobNo"] }
            };

            var result = await _salesinvoice.GetInvoices(filters);

            return Json(result);
        }

        public async Task<IActionResult> GetSalesInvoiceById(int invoiceId)
        {
            var result = await _salesinvoice.GetInvoiceById(invoiceId);

            return Json(result);
        }

        public async Task<IActionResult> EditSalesInvoice(int? id)
        {
            if (id == null)
                return NotFound();

            bool isEditable = await _salesinvoice.IsInvoiceEditable(id.Value);

            if (!isEditable)
            {
                TempData["ErrorMessage"] = "This invoice cannot be edited because payment has already been received.";
                return RedirectToAction(nameof(SalesInvoice)); // Apna list action
            }

            ViewBag.Id = id;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> EditSalesInvoice(int invoiceId)
        {
            var result = await _salesinvoice.EditSalesInvoice(invoiceId);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        [HttpPost]
        public async Task<IActionResult> SavePaymentReceived(PaymentReceived model)
        {
            var result = await _salesinvoice.SavePaymentReceived(model);

            return Content(
                JsonConvert.SerializeObject(result),
                "application/json"
            );
        }

        [HttpGet]
        public async Task<IActionResult> GetNextCreditNoteNumber()
        {
            var result = await _salesinvoice.GetNextCreditNoteNumber();

            return Content(
                JsonConvert.SerializeObject(result),
                "application/json"
            );
        }

        [HttpPost]
        public async Task<IActionResult> CancelInvoice(int invoiceId)
        {
            var result = await _salesinvoice.CancelInvoice(invoiceId);

            return Content(
                JsonConvert.SerializeObject(result),
                "application/json"
            );
        }

        [HttpGet]
        public async Task<IActionResult> GetCreditNoteSummary(int invoiceId)
        {
            var result = await _salesinvoice.GetCreditNoteSummary(invoiceId);

            return Content(
                JsonConvert.SerializeObject(result),
                "application/json"
            );
        }

        [HttpGet]
        public async Task<IActionResult> GetNextSalesInvoiceNumber()
        {
            var result = await _salesinvoice.GetNextSalesInvoiceNumber();

            return Content(
                JsonConvert.SerializeObject(result),
                "application/json");
        }
        #endregion

        #region Purchase Invoice

        public IActionResult CreatePurchaseInvoice()
        {
            return View();
        }

        public IActionResult PurchaseInvoice()
        {
            return View();
        }

        public IActionResult PurchaseInvoiceView(int? id, int? serviceProviderId)
        {
            ViewBag.Id = id;
            ViewBag.ServiceProviderId = serviceProviderId;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SavePurchaseInvoice([FromBody] PurchaseInvoiceVM model)
        {
            var result = await _purchaseinvoice.Save(model, _session.LoginId);

            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<IActionResult> GetPurchaseInvoices()
        {
            var filters = new List<QueryFilters>
            {
                new QueryFilters { fieldName = "draw", filterValue = Request.Form["draw"] },
                new QueryFilters { fieldName = "start", filterValue = Request.Form["start"] },
                new QueryFilters { fieldName = "length", filterValue = Request.Form["length"] },
                new QueryFilters { fieldName = "search", filterValue = Request.Form["search[value]"] },

                new QueryFilters { fieldName = "orderColumn", filterValue = Request.Form["order[0][column]"] },
                new QueryFilters { fieldName = "orderDir", filterValue = Request.Form["order[0][dir]"] },

                new QueryFilters { fieldName = "ServiceProviderId", filterValue = Request.Form["serviceProviderId"] },
                new QueryFilters { fieldName = "FromDate", filterValue = Request.Form["fromDate"] },
                new QueryFilters { fieldName = "ToDate", filterValue = Request.Form["toDate"] },
                new QueryFilters { fieldName = "InvoiceNo", filterValue = Request.Form["invoiceNo"] },
                new QueryFilters { fieldName = "JobNo", filterValue = Request.Form["jobNo"] }
            };

            var result = await _purchaseinvoice.GetInvoices(filters);

            return Json(result);
        }

        public async Task<IActionResult> GetPurchaseInvoiceById(int invoiceId, int serviceProviderId)
        {
            var result = await _purchaseinvoice.GetInvoiceById(invoiceId, serviceProviderId);

            return Json(result);
        }

        public async Task<IActionResult> EditPurchaseInvoice(int? id)
        {
            if (id == null)
                return NotFound();

            bool isEditable = await _purchaseinvoice.IsInvoiceEditable(id.Value);

            if (!isEditable)
            {
                TempData["ErrorMessage"] = "This purchase invoice cannot be edited because payment has already been made.";
                return RedirectToAction(nameof(PurchaseInvoice)); // Apna listing action
            }

            ViewBag.Id = id;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> EditPurchaseInvoice(int invoiceId)
        {
            var result = await _purchaseinvoice.EditPurchaseInvoice(invoiceId);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        [HttpPost]
        public async Task<IActionResult> SavePurchaseVoucher(PurchaseVoucher model)
        {
            var result = await _purchaseinvoice.SavePurchaseVoucher(model);

            return Content(
                JsonConvert.SerializeObject(result),
                "application/json"
            );
        }

        [HttpGet]
        public async Task<IActionResult> GetNextCreditNoteNumberPurchase()
        {
            var result = await _purchaseinvoice.GetNextCreditNoteNumber();

            return Content(
                JsonConvert.SerializeObject(result),
                "application/json"
            );
        }

        [HttpPost]
        public async Task<IActionResult> CancelInvoicePurchase(int invoiceId)
        {
            var result = await _purchaseinvoice.CancelInvoice(invoiceId);

            return Content(
                JsonConvert.SerializeObject(result),
                "application/json"
            );
        }

        [HttpGet]
        public async Task<IActionResult> GetCreditNoteSummaryPurchase(int invoiceId)
        {
            var result = await _purchaseinvoice.GetCreditNoteSummary(invoiceId);

            return Content(
                JsonConvert.SerializeObject(result),
                "application/json"
            );
        }

        [HttpGet]
        public async Task<IActionResult> GetNextPurchaseInvoiceNumber()
        {
            var result = await _purchaseinvoice.GetNextPurchaseInvoiceNumber();

            return Content(
                JsonConvert.SerializeObject(result),
                "application/json");
        }

        #endregion

        #region Government Tax

        public IActionResult GovtTax()
        {
            return View();
        }

        public async Task<IActionResult> GetGovernmentTaxReport()
        {
            var filters = new List<QueryFilters>
            {
                new QueryFilters { fieldName = "FromDate", filterValue = Request.Form["fromDate"] },
                new QueryFilters { fieldName = "ToDate", filterValue = Request.Form["toDate"] },
                
            };

            var result = await _salesinvoice.GetGovernmentTaxReport(filters);
            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadGovernmentTaxPdf(DateTime fromDate, DateTime toDate)
        {
            var pdfBytes = await _salesinvoice.DownloadGovernmentTaxPdf(fromDate, toDate);

            return File(
                pdfBytes,
                "application/pdf",
                $"GovernmentTax_{DateTime.Now:yyyyMMdd}.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> DownloadGovernmentTaxExcel(DateTime fromDate, DateTime toDate)
        {
            byte[] fileBytes = await _salesinvoice.DownloadGovernmentTaxExcel(fromDate, toDate);

            string fileName = $"Government_Tax_Report_{fromDate:yyyyMMdd}_to_{toDate:yyyyMMdd}.xlsx";

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        #endregion
    }
}