using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using BusinessLogic.Services;
using Microsoft.EntityFrameworkCore;

namespace JobApplication.Controllers
{
    public class CustomerController : BaseController
    {
        private readonly ICompany _company;
        private readonly ICustomer _customer;
        private readonly ISessionHelper _session;

        public CustomerController(ICustomer customer, ICompany company, ISessionHelper session) : base(session)
        {
            _customer = customer;
            _company = company;
            _session = session;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SaveCompany(string companyName, string companyNameAr)
        {
            var result = await _company.SaveCompanyAsync(companyName, companyNameAr);

            return Json(new { id = result.Id, name = result.Name, nameAr = result.NameAr });
        }

        public async Task<IActionResult> Save(Customer model, IFormFile PhotoFile)
        {
            var result = await _customer.Save(model);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<IActionResult> GetCustomers()
        {
            var draw = Request.Form["draw"].FirstOrDefault();
            var start = Request.Form["start"].FirstOrDefault();
            var length = Request.Form["length"].FirstOrDefault();
            var search = Request.Form["search[value]"].FirstOrDefault();


            var filters = new List<QueryFilters>
            {
                new QueryFilters { fieldName = "draw", filterValue = draw },
                new QueryFilters { fieldName = "start", filterValue = start },
                new QueryFilters { fieldName = "length", filterValue = length },
                new QueryFilters { fieldName = "search", filterValue = search },

                new QueryFilters { fieldName = "orderColumn", filterValue = Request.Form["order[0][column]"] },
                new QueryFilters { fieldName = "orderDir", filterValue = Request.Form["order[0][dir]"] },

                new QueryFilters { fieldName = "CustomerName", filterValue = Request.Form["customerName"] },
                new QueryFilters { fieldName = "Email", filterValue = Request.Form["email"] },
                new QueryFilters { fieldName = "VatId", filterValue = Request.Form["vatId"] },
                new QueryFilters { fieldName = "IsActive", filterValue = Request.Form["status"] }
            };

            var result = await _customer.GetCustomers(filters);

            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<IActionResult> GetCustomerById(int id)
        {
            var result = await _customer.GetCustomerById(id);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<IActionResult> GetServiceProviderById(int id)
        {
            var result = await _customer.GetServiceProviderById(id);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<IActionResult> ToggleCustomerStatus(int id)
        {
            var result = await _customer.ToggleCustomerStatus(id);

            return Content(JsonConvert.SerializeObject(result), "application/json");
        }
    }
}
