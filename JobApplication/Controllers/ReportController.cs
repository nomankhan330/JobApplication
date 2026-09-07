using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JobApplication.Controllers
{
    public class ReportController : BaseController
    {
        private readonly ISessionHelper _session;
        private readonly IJobImportMaster _jobimportmaster;
        private readonly ICostCenter _costCenter;
        private readonly ISalesInvoice _salesInvoice;
        private readonly IPurchaseInvoice _purchaseInvoice;
        private readonly IAccount _account;

        public ReportController(
    IJobImportMaster jobImportMaster,
    ICostCenter costCenter,
    ISalesInvoice salesInvoice,
    IPurchaseInvoice purchaseInvoice,
    IAccount account,
    ISessionHelper session)
    : base(session)
{
    _jobimportmaster = jobImportMaster;
    _costCenter = costCenter;
    _salesInvoice = salesInvoice;
    _purchaseInvoice = purchaseInvoice;
    _account = account;
    _session = session;
}

        public IActionResult CostCenter()
        {
            return View();
        }

        public IActionResult JobReport()
        {
            return View();
        }

        public IActionResult SalesReport()
        {
            return View();
        }

        public async Task<IActionResult> GetJobsReport()
        {
            var filters = new List<QueryFilters>
            {
                new QueryFilters { fieldName = "draw", filterValue = Request.Form["draw"] },
                new QueryFilters { fieldName = "start", filterValue = Request.Form["start"] },
                new QueryFilters { fieldName = "length", filterValue = Request.Form["length"] },
                new QueryFilters { fieldName = "search", filterValue = Request.Form["search[value]"] },

                new QueryFilters { fieldName = "orderColumn", filterValue = Request.Form["order[0][column]"] },
                new QueryFilters { fieldName = "orderDir", filterValue = Request.Form["order[0][dir]"] },

                new QueryFilters { fieldName = "UserId", filterValue = Request.Form["userId"] },
                new QueryFilters { fieldName = "FromDate", filterValue = Request.Form["fromDate"] },
                new QueryFilters { fieldName = "ToDate", filterValue = Request.Form["toDate"] },
                new QueryFilters { fieldName = "BLNumber", filterValue = Request.Form["blNumber"] },
                new QueryFilters { fieldName = "ShipmentType", filterValue = Request.Form["shipmentType"] }
            };

            var result = await _jobimportmaster.GetJobsReport(filters);

            return Json(result);
        }

        [HttpPost]
        public async Task<IActionResult> GetCostCenterReport()
        {
            var filters = Request.Form.Keys
                .Select(k => new QueryFilters
                {
                    fieldName = k,
                    filterValue = Request.Form[k]
                }).ToList();

            var result = await _costCenter.GetCostCenterReport(filters);

            return Json(result);
        }

        [HttpPost]
        public async Task<IActionResult> GetSalesReport()
        {
            var filters = Request.Form.Keys
                .Select(k => new QueryFilters
                {
                    fieldName = k,
                    filterValue = Request.Form[k]
                }).ToList();

            var result = await _salesInvoice.GetSalesReport(filters);

            return Json(result);
        }

        public IActionResult CustomerSOA()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetCustomerStatement()
        {
            var filters = Request.Form.Keys
                .Select(k => new QueryFilters
                {
                    fieldName = k,
                    filterValue = Request.Form[k]
                }).ToList();

            var result = await _salesInvoice.GetCustomerStatement(filters);

            return Json(result);
        }

        public IActionResult ServiceProviderSOA()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetServiceProviderStatement()
        {
            var filters = Request.Form.Keys
                .Select(k => new QueryFilters
                {
                    fieldName = k,
                    filterValue = Request.Form[k]
                }).ToList();

            var result = await _purchaseInvoice.GetServiceProviderStatement(filters);

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetCompanyNames()
        {
            if (_session.LoginId == 0)
            {
                return Unauthorized(new
                {
                    errorCode = 401,
                    errorMessage = "Session expired"
                });
            }

            var result = await _account.GetCompanyNamesAsync();

            return Json(new
            {
                errorCode = 200,
                companyName = result.CompanyName,
                companyNameAr = result.CompanyNameAr
            });
        }
    }
}
