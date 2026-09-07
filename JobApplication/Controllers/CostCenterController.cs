using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using BusinessLogic.Services;

namespace JobApplication.Controllers
{
    public class CostCenterController : BaseController
    {
        private readonly ICostCenter _costcenter;
        private readonly ISessionHelper _session;

        public CostCenterController(ICostCenter costCenter, ISessionHelper session)
            : base(session)
        {
            _costcenter = costCenter;
            _session = session;
        }

        public async Task<IActionResult> SaveCostCenter(CostCenter model, IFormFile PhotoFile)
        {
            if (_session.LoginId == 0)
            {
                return RedirectToAction("Login", "Home");
            }

            // Handle Photo Upload
            if (PhotoFile != null && PhotoFile.Length > 0)
            {
                string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/costcenters");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string fileName = $"{Guid.NewGuid()}{Path.GetExtension(PhotoFile.FileName)}";
                string filePath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await PhotoFile.CopyToAsync(stream);
                }

                model.Photo = fileName;
            }

            model.CreatedBy = _session.LoginId;
            model.CreatedOn = DateTime.Now;

            var result = await _costcenter.SaveCostCenter(model);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        [HttpPost]
        public async Task<IActionResult> GetCostCenters()
        {
            var filters = new List<QueryFilters>
            {
                new QueryFilters { fieldName = "draw", filterValue = Request.Form["draw"] },
                new QueryFilters { fieldName = "start", filterValue = Request.Form["start"] },
                new QueryFilters { fieldName = "length", filterValue = Request.Form["length"] },
                new QueryFilters { fieldName = "search", filterValue = Request.Form["search[value]"] },

                // 🔥 Sorting
                new QueryFilters { fieldName = "orderColumn", filterValue = Request.Form["order[0][column]"] },
                new QueryFilters { fieldName = "orderDir", filterValue = Request.Form["order[0][dir]"] },

                // 🔍 Filters
                new QueryFilters { fieldName = "CostCenterName", filterValue = Request.Form["name"] },
                new QueryFilters { fieldName = "CostCenterCode", filterValue = Request.Form["code"] },
                new QueryFilters { fieldName = "ContactNumber", filterValue = Request.Form["contact"] },
                new QueryFilters { fieldName = "IsActive", filterValue = Request.Form["status"] }
            };

            var result = await _costcenter.GetCostCenters(filters);

            return Json(result);
        }

        public async Task<IActionResult> GetCostCenterById(int id)
        {
            var result = await _costcenter.GetCostCenterById(id);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<IActionResult> DeleteCostCenterById(int id)
        {
            var result = await _costcenter.DeleteCostCenterById(id);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<IActionResult> ToggleCostCenterStatus(int id)
        {
            var result = await _costcenter.ToggleCostCenterStatus(id);

            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}
