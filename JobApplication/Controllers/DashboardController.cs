using BusinessLogic.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace JobApplication.Controllers
{
    public class DashboardController : BaseController
    {
        //private readonly IDashboard _service;
        private readonly ISessionHelper _session;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IJobImportMaster _jobImportMaster;

        public DashboardController(ISessionHelper session, IWebHostEnvironment webHostEnvironment, IJobImportMaster jobImportMaster) : base(session)
        {
            //_service = service;
            _session = session;
            _webHostEnvironment = webHostEnvironment;
            _jobImportMaster = jobImportMaster;
        }

        public IActionResult Index2()
        {
            return View();
        }

        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Complete()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetStatistics()
        {
            // Use IJobImportMaster service to compute counts
            try
            {
                // helper to call GetJobs and extract total count
                async Task<int> CountAsync(string shipmentType = null)
                {
                    ArgumentNullException.ThrowIfNull(shipmentType);
                    var filters = new List<BusinessLogic.Models.QueryFilters>
                    {
                        new BusinessLogic.Models.QueryFilters { fieldName = "draw", filterValue = "1" },
                        new BusinessLogic.Models.QueryFilters { fieldName = "start", filterValue = "0" },
                        new BusinessLogic.Models.QueryFilters { fieldName = "length", filterValue = "1" }
                    };

                    if (!string.IsNullOrEmpty(shipmentType))
                    {
                        filters.Add(new BusinessLogic.Models.QueryFilters { fieldName = "ShipmentType", filterValue = shipmentType });
                    }

                    dynamic result = await _jobImportMaster.GetJobs(filters);

                    int total = 0;
                    try
                    {
                        total = Convert.ToInt32(result.recordsTotal ?? 0);
                    }
                    catch { }

                    return total;
                }

                int totalJobs = await CountAsync();
                int importJobs = await CountAsync("1");
                int exportJobs = await CountAsync("2");

                var data = new
                {
                    totalJobs,
                    importJobs,
                    exportJobs,

                    // Trends left as placeholders — replace with proper calculations if required
                    totalJobsTrendText = "",
                    importJobsTrendText = "",
                    exportJobsTrendText = "",

                    totalJobsTrendClass = "",
                    importJobsTrendClass = "",
                    exportJobsTrendClass = ""
                };

                return Json(data);
            }
            catch (Exception ex)
            {
                return Json(new { error = true, message = ex.Message });
            }
        }
    }
}
