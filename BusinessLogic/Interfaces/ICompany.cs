using BusinessLogic.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Interfaces
{
    public interface ICompany : IDisposable
    {
        //public Task<dynamic> SaveCompany(Company model);
        public Task<(int Id, string Name, string NameAr)> SaveCompanyAsync(string companyName, string companyNameAr);
    }
}
