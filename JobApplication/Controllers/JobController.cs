using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace JobApplication.Controllers
{
    public class JobController : BaseController
    {
        private readonly IPol _pol;
        private readonly IPod _pod;
        private readonly IDeliveryCity _deliverycity;
        private readonly IJobImportMaster _jobimportmaster;

        private readonly ISessionHelper _session;

        public JobController(IPol pol, IPod pod, IDeliveryCity deliveryCity, IJobImportMaster jobImportMaster, ISessionHelper session) : base(session)
        {
            _pol = pol;
            _pod = pod;
            _deliverycity = deliveryCity;
            _jobimportmaster = jobImportMaster;
            _session = session;
        }

        public IActionResult Import()
        {
            ViewBag.LoginType = _session.LoginType;
            return View();
        }

        public async Task<IActionResult> AddImport(int? id)
        {
            ViewBag.EditId = id;
            ViewBag.UserType = _session.UserType;

            int userId = _session.LoginId;

            if (_session.UserType == 3 || _session.UserType == 4)
            {
                // UserType 4 cannot access Add page without an Id or add this check when unauth user not creating a job
                //if (_session.UserType == 4 && (!id.HasValue || id.Value <= 0))
                //{
                //    return RedirectToAction("Import", "Job");
                //}

                // Access checks only when editing
                if (id > 0)
                {
                    bool allowed = await _jobimportmaster.CanAccessJob(id.Value, userId)
                                && await _jobimportmaster.CanAccessInvoice(id.Value, userId);

                    if (!allowed)
                    {
                        return RedirectToAction("Import", "Job");
                    }
                }
            }

            return View();
        }

        //public async Task<IActionResult> AddImport(int? id)
        //{
        //    ViewBag.EditId = id;
        //    ViewBag.LoginType = _session.LoginType;

        //    int userId = _session.LoginId;

        //    if(_session.UserType == 3 || _session.UserType == 4)
        //    {
        //        if(id > 0)
        //        {
        //            bool allowedJob = await _jobimportmaster.CanAccessJob(id ?? 0, userId);

        //            if (!allowedJob)
        //            {
        //                return RedirectToAction("Import", "Job");
        //            }
        //            bool allowed = await _jobimportmaster.CanAccessInvoice(id ?? 0, userId);

        //            if (!allowed)
        //            {
        //                return RedirectToAction("Import", "Job");
        //            }
        //        }
        //    }

        //    return View();
        //}

        public IActionResult Export()
        {
            ViewBag.LoginType = _session.LoginType;
            return View();
        }

        public async Task<IActionResult> AddExport(int? id)
        {
            ViewBag.EditId = id;
            ViewBag.UserType = _session.UserType;

            int userId = _session.LoginId;

            if (_session.UserType == 3 || _session.UserType == 4)
            {
                // UserType 4 cannot access Add page without an Id or add this check when unauth user not creating a job
                //if (_session.UserType == 4 && (!id.HasValue || id.Value <= 0))
                //{
                //    return RedirectToAction("Export", "Job");
                //}

                // Access checks only when editing
                if (id > 0)
                {
                    bool allowed = await _jobimportmaster.CanAccessJob(id.Value, userId)
                                && await _jobimportmaster.CanAccessInvoice(id.Value, userId);

                    if (!allowed)
                    {
                        return RedirectToAction("Export", "Job");
                    }
                }
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SavePol(string portName)
        {

            var result = await _pol.SavePolAsync(portName);

            return Json(new { id = result.Id, name = result.Name });
        }

        [HttpPost]
        public async Task<IActionResult> SavePod(string portName)
        {

            var result = await _pod.SavePodAsync(portName);

            return Json(new { id = result.Id, name = result.Name });
        }

        [HttpPost]
        public async Task<IActionResult> SaveDeliveryCity(string deliveryCityName)
        {

            var result = await _deliverycity.SaveDeliveryCityAsync(deliveryCityName);

            return Json(new { id = result.Id, name = result.Name });
        }

        [HttpPost]
        public async Task<IActionResult> SaveJobImportMaster(JobImportMasterVM model)
        {
            var result = await _jobimportmaster.Save(model);

            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        [HttpPost]
        public async Task<IActionResult> SaveJobImportMasterInvoice(JobImportMasterVM model)
        {
            var result = await _jobimportmaster.SaveInvoicePayments(model, model.Id, model.JobNumber, true);

            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<IActionResult> GetImportJobs()
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

            var result = await _jobimportmaster.GetJobs(filters);

            return Json(result);
        }

        [HttpPost]
        public async Task<IActionResult> GetAllJobById(int id)
        {
            var result = await _jobimportmaster.GetAllJobById(id);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        [HttpPost]
        public async Task<IActionResult> GetJobById(int id, int? paymentTypeId = null)
        {
            var result = await _jobimportmaster.GetJobById(id, paymentTypeId);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        [HttpPost]
        public async Task<IActionResult> CheckInvoiceAccess(int jobId)
        {
            var result = await _jobimportmaster.CheckInvoiceAccess(jobId, _session.LoginId);

            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        [HttpPost]
        public async Task<IActionResult> RequestInvoiceAccess(int jobId)
        {
            var result = await _jobimportmaster.RequestInvoiceAccess(jobId);

            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        [HttpGet]
        public IActionResult InvoiceAccessRequests()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetInvoiceAccessRequests()
        {
            var data = await _jobimportmaster.GetPendingRequests();

            return Content(JsonConvert.SerializeObject(data), "application/json");
        }

        [HttpPost]
        public async Task<IActionResult> ApproveInvoiceAccess(int requestId)
        {
            var data = await _jobimportmaster.ApproveInvoiceAccess(requestId);

            return Content(JsonConvert.SerializeObject(data), "application/json");
        }
    }
}
