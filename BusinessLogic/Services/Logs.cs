using BusinessLogic.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;

namespace BusinessLogic.Services
{
    public class Logs : ILogs
    {
        private readonly IDatabaseObject _db;

        public Logs(IDatabaseObject db)
        {
            _db = db;
        }

        public void Dispose()
        {

        }

        public void Write(string message)
        {
            Write(new Models.Logs { Error = message });
        }

        public void Write(string pageName, string functionName, string message)
        {
            Write(new Models.Logs
            {
                PageName = pageName,
                FunctionName = functionName,
                Error = message
            });
        }

        public void Write(string pageName, string functionName, Exception e)
        {
            string message = e.Message;
            if (e.InnerException != null)
            {
                message = e.InnerException.Message;
            }

            Write(new Models.Logs
            {
                PageName = pageName,
                FunctionName = functionName,
                Error = message
            });
        }

        public void Write(Models.Logs logs)
        {
            logs.LogDate = DateTime.Now;
            logs.LogId = Guid.NewGuid();

            SqlParameter[] param = 
            {
                new SqlParameter { ParameterName = "@LogId", Value = logs.LogId },
                new SqlParameter { ParameterName = "@LogDate", Value = logs.LogDate },
                new SqlParameter { ParameterName = "@PageName", Value = logs.PageName },
                new SqlParameter { ParameterName = "@FunctionName", Value = logs.FunctionName },
                new SqlParameter { ParameterName = "@ErrorType", Value = logs.ErrorType == null ? "General" : logs.ErrorType },
                new SqlParameter { ParameterName = "@Error", Value = logs.Error }
            };

            string sql = "insert into Logs values (@LogId, @LogDate, @PageName, @FunctionName, @ErrorType, @Error)";
            _db.Execute(sql, param, CommandType.Text);
        }
    }
}
