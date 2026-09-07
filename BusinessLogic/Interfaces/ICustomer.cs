using BusinessLogic.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Interfaces
{
    public interface ICustomer : IDisposable
    {
        public Task<dynamic> Save(Customer model);

        Task<dynamic> GetCustomers(IList<QueryFilters> filters);

        Task<dynamic> GetCustomerById(int id);

        Task<dynamic> GetServiceProviderById(int id);

        Task<dynamic> DeleteCustomerById(int id);

        Task<dynamic> SetCustomerStatus(int id, bool isActive);

        Task<dynamic> ToggleCustomerStatus(int id);
    }
}
