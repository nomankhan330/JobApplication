using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Interfaces
{
    public interface IConnectionStringManager
    {
        string GetConnectionString(string dataSource, string userId, string password, string database);
    }

    public class ConnectionStringManager : IConnectionStringManager
    {
        private readonly IConfiguration _configuration;

        public ConnectionStringManager(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GetConnectionString(string dataSource, string userId, string password, string database)
        {
            var baseConnectionString = _configuration.GetConnectionString("AppCon");
            if (dataSource == "")
            {
                return baseConnectionString;
            }

            var builder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                DataSource = dataSource, 
                UserID = userId,
                Password = password,
                InitialCatalog = database // Set the database based on the selected company
            };
            return builder.ConnectionString;
        }
    }
}
