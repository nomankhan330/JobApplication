using BusinessLogic.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusinessLogic.Interfaces
{
    public interface IAccount : IDisposable
    {
        Task<dynamic> Login(string username, string password);

        #region User
        Task<dynamic> GetUser(IList<QueryFilters> filters);
        Task<dynamic> SaveUser(User model);
        Task<dynamic> GetUserById(int Id);
        Task<dynamic> DeleteUserById(int Id);
        Task<dynamic> ToggleUserStatus(int id);
        #endregion User

        #region Profile
        Task<dynamic> GetProfile();
        Task<dynamic> UpdateProfile(User model);
        Task<(string CompanyName, string CompanyNameAr)> GetCompanyNamesAsync();
        #endregion Profile
    }
}
