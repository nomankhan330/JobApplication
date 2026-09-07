using BusinessLogic.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Interfaces
{
    public interface ICostCenter : IDisposable
    {
        public Task<dynamic> GetCostCenters(IList<QueryFilters> filters);
        public Task<dynamic> SaveCostCenter(CostCenter model);
        public Task<dynamic> GetCostCenterById(int id);
        public Task<dynamic> DeleteCostCenterById(int id);
        public Task<dynamic> ToggleCostCenterStatus(int id);
        public Task<dynamic> GetCostCenterReport(IList<QueryFilters> filters);
    }
}
