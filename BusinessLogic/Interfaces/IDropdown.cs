using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Interfaces
{
    public interface IDropdown
    {
        public Task<dynamic> GetUser();
        public Task<dynamic> GetAdmin();
        public Task<dynamic> GetUserType();
        public Task<dynamic> GetCostCenter();
        public Task<dynamic> GetCostCenterAssigned();
        public Task<dynamic> GetCountry();
        public Task<dynamic> GetRegion();
        public Task<dynamic> GetCompany();
        public Task<dynamic> GetCustomer();
        public Task<dynamic> GetCustomerSearch(string search = "", int page = 1);
        public Task<dynamic> GetServiceProvider();
        public Task<dynamic> GetServiceProviderSearch(string search = "", int page = 1);
        public Task<dynamic> GetShipmentMode();
        public Task<dynamic> GetJobType();
        public Task<dynamic> GetContainerType();
        public Task<dynamic> GetBLType();
        public Task<dynamic> GetPol();
        public Task<dynamic> GetPod();
        public Task<dynamic> GetDeliveryCity();
        public Task<dynamic> GetShipmentStatus();
        public Task<dynamic> GetBLStatus();
        public Task<dynamic> GetPaymentHeadersByType(int paymentTypeId);
        public Task<dynamic> GetJobNos(string search = "", int page = 1);
    }
}
