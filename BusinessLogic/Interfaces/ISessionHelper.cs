using BusinessLogic.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Interfaces
{
    public interface ISessionHelper
    {
        int AccountId { get; set; }
        string AccountName { get; set; }
        int LoginId { get; set; }
        int LoginType { get; set; }
        int UserType { get; set; }
        string UserName { get; set; }
        string CostCenters { get; set; }
        string CostCenterIds { get; set; }
        string Token {  get; set; }
        string Datasource { get; set; }
        string Databasename { get; set; }
        DateTime? ExpiryDate { get; set; }
        string Dbpassword { get; set; }
        string Dbuserid { get; set; }
        int ReferenceId { get; set; }
        string Photo { get; set; }

        void Set(string key, string value);

        string Get(string key);

        void Login(int AccountId, int LoginId, string UserName, int ReferenceId, string Photo);
        void Logout();
        
    }
}
