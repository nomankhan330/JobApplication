using Microsoft.Data.SqlClient;
using BusinessLogic.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Interfaces
{
    public interface IDatabaseObject
    {
        public Task<(DataTableCollection tables, ErrorResponse error)> ExecuteAndFetch(string query, SqlParameter[] sqlprms);
        public Task<(DataTableCollection tables, ErrorResponse error)> ExecuteAndFetch(string query, SqlParameter[] sqlprms, CommandType commandType);
        public Task<ErrorResponse> Execute(string query, SqlParameter[] sqlprms);
        public Task<ErrorResponse> Execute(string query, SqlParameter[] sqlprms, CommandType commandType);
        public Task<(DataTableCollection tables, ErrorResponse error)> Fetch(string query, SqlParameter[] sqlprms);
        public Task<(DataTableCollection tables, ErrorResponse error)> Fetch(string query, SqlParameter[] sqlprms, CommandType commandType);
        public Task<(DataTable table, ErrorResponse error)> ExecuteAndFetchTable(string query, SqlParameter[] sqlprms);
        public Task<(DataTable table, ErrorResponse error)> ExecuteAndFetchTable(string query, SqlParameter[] sqlprms, CommandType commandType);
        public Task<(DataTable table, ErrorResponse error)> FetchTable(string query, SqlParameter[] sqlprms);
        public Task<(DataTable table, ErrorResponse error)> FetchTable(string query, SqlParameter[] sqlprms, CommandType commandType);
        public Task<string> DLookup(string query);
        public Task<(string value, ErrorResponse error)> DLookup_response(string query);
        public Task<(string value, ErrorResponse error)> DLookup(string query, SqlParameter[] sqlprms);
        public Task<(string value, ErrorResponse error)> DLookup(string query, SqlParameter[] sqlprms, CommandType commandType);

        public string GetConnectionString();

        public Task<(DataTableCollection tables, ErrorResponse error)> ExecuteAndFetch(string query, SqlParameter[] sqlprms, CommandType commandType, SqlConnection sqlCon, SqlTransaction sqlTrans);
        public Task<ErrorResponse> Execute(string query, SqlParameter[] sqlprms, CommandType commandType, SqlConnection sqlCon, SqlTransaction sqlTrans);
        public Task<(DataTableCollection tables, ErrorResponse error)> Fetch(string query, SqlParameter[] sqlprms, CommandType commandType, SqlConnection sqlCon, SqlTransaction sqlTrans);
        public Task<(DataTable table, ErrorResponse error)> ExecuteAndFetchTable(string query, SqlParameter[] sqlprms, CommandType commandType, SqlConnection sqlCon, SqlTransaction sqlTrans);
        public Task<(DataTable table, ErrorResponse error)> FetchTable(string query, SqlParameter[] sqlprms, CommandType commandType, SqlConnection sqlCon, SqlTransaction sqlTrans);
        public Task<(string value, ErrorResponse error)> DLookup(string query, SqlParameter[] sqlprms, CommandType commandType, SqlConnection sqlCon, SqlTransaction sqlTrans);
    }
}
