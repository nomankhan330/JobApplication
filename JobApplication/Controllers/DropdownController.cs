using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace JobApplication.Controllers
{
    public class DropdownController : Controller
    {
        private readonly IDropdown _dropdown;

        public DropdownController(IDropdown dropdown)
        {
            _dropdown = dropdown;
        }

        public async Task<ContentResult> GetUser()
        {
            var result = await _dropdown.GetUser();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetAdmin()
        {
            var result = await _dropdown.GetAdmin();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetUserType()
        {
            var result = await _dropdown.GetUserType();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetCostCenter()
        {
            var result = await _dropdown.GetCostCenter();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetCostCenterAssigned()
        {
            var result = await _dropdown.GetCostCenterAssigned();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetCountry()
        {
            var result = await _dropdown.GetCountry();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetRegion()
        {
            var result = await _dropdown.GetRegion();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetCompany()
        {
            var result = await _dropdown.GetCompany();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetCustomer()
        {
            var result = await _dropdown.GetCustomer();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetCustomerSearch(string search = "", int page = 1)
        {
            var result = await _dropdown.GetCustomerSearch(search, page);

            return Content(
                JsonConvert.SerializeObject(result),
                "application/json"
            );
        }

        public async Task<ContentResult> GetServiceProvider()
        {
            var result = await _dropdown.GetServiceProvider();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetServiceProviderSearch(string search = "", int page = 1)
        {
            var result = await _dropdown.GetServiceProviderSearch(search, page);

            return Content(
                JsonConvert.SerializeObject(result),
                "application/json"
            );
        }

        public async Task<ContentResult> GetShipmentMode()
        {
            var result = await _dropdown.GetShipmentMode();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetJobType()
        {
            var result = await _dropdown.GetJobType();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetContainerType()
        {
            var result = await _dropdown.GetContainerType();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetBLType()
        {
            var result = await _dropdown.GetBLType();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetPol()
        {
            var result = await _dropdown.GetPol();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetPod()
        {
            var result = await _dropdown.GetPod();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetDeliveryCity()
        {
            var result = await _dropdown.GetDeliveryCity();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetShipmentStatus()
        {
            var result = await _dropdown.GetShipmentStatus();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetBLStatus()
        {
            var result = await _dropdown.GetBLStatus();
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetPaymentHeadersByType(int paymentTypeId)
        {
            var result = await _dropdown.GetPaymentHeadersByType(paymentTypeId);
            return Content(JsonConvert.SerializeObject(result), "application/json");
        }

        public async Task<ContentResult> GetJobNos(string search = "", int page = 1)
        {
            var result = await _dropdown.GetJobNos(search, page);

            return Content(JsonConvert.SerializeObject(result), "application/json");
        }
    }
}
