using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using BusinessLogic.Helper;
using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace BusinessLogic.Services
{
    public class DatabaseObject : IDatabaseObject
    {
        private string _connectionString;
        private readonly ISessionHelper _session;

        public DatabaseObject(string connectionString, ISessionHelper session)
        {
            _session = session;
            _connectionString = connectionString;
        }

        public async Task<(DataTableCollection tables, ErrorResponse error)> ExecuteAndFetch(string query, SqlParameter[] sqlprms)
        {
            return await ExecuteAndFetch(query, sqlprms, CommandType.Text);
        }

        public async Task<(DataTableCollection tables, ErrorResponse error)> ExecuteAndFetch(string query, SqlParameter[] sqlprms, CommandType commandType)
        {
            ErrorResponse strError = new ErrorResponse();
            using (SqlConnection SqlCon = new SqlConnection(_connectionString))
            {
                if (SqlCon.State == ConnectionState.Open)
                {
                    await SqlCon.CloseAsync();
                }

                await SqlCon.OpenAsync();

                using (SqlTransaction SqlTrans = SqlCon.BeginTransaction())
                {
                    try
                    {
                        DataSet ds = new DataSet("DataSet1");
                        using (SqlCommand cmd = new SqlCommand(query, SqlCon))
                        {
                            cmd.CommandType = commandType;
                            cmd.Transaction = SqlTrans;
                            if (sqlprms != null)
                            {
                                foreach (SqlParameter sqlp in sqlprms)
                                    cmd.Parameters.Add(sqlp);
                            }

                            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                            {
                                do
                                {
                                    DataTable dt = new DataTable();
                                    dt.Load(reader);
                                    ds.Tables.Add(dt);
                                }
                                while (await reader.NextResultAsync());

                                // Now the DataSet 'ds' contains multiple DataTables
                            }

                        }

                        SqlTrans.Commit();
                        return (ds.Tables, strError);
                    }
                    catch (SqlException ae)
                    {
                        SqlTrans.Rollback();
                        strError.Error = true;
                        strError.ErrorList.Add(new ErrorList() { ErrorCode = ae.ErrorCode, Message = ae.Message, ErrorType = "SQL", WarningLevel = Level.Error });

                        //_logs.Write("DatabaseObject", "ExecuteAndFetch", ae);

                        return (null, strError);
                    }
                    catch (Exception ae)
                    {
                        SqlTrans.Rollback();
                        strError.Error = true;
                        strError.ErrorList.Add(new ErrorList() { ErrorCode = 999, Message = ae.Message, ErrorType = "General", WarningLevel = Level.Error });

                        //_logs.Write("DatabaseObject", "ExecuteAndFetch", ae);

                        return (null, strError);
                    }
                }
            }
        }

        public async Task<ErrorResponse> Execute(string query, SqlParameter[] sqlprms)
        {
            return await Execute(query, sqlprms, CommandType.Text);
        }

        public async Task<ErrorResponse> Execute(string query, SqlParameter[] sqlprms, CommandType commandType)
        {
            ErrorResponse strError = new ErrorResponse();
            using (SqlConnection SqlCon = new SqlConnection(_connectionString))
            {
                if (SqlCon.State == ConnectionState.Open)
                {
                    await SqlCon.CloseAsync();
                }

                await SqlCon.OpenAsync();

                using (SqlTransaction SqlTrans = SqlCon.BeginTransaction())
                {
                    try
                    {
                        DataSet ds = new DataSet("DataSet1");
                        using (SqlCommand cmd = new SqlCommand(query, SqlCon))
                        {
                            cmd.CommandType = commandType;
                            cmd.Transaction = SqlTrans;
                            if (sqlprms != null)
                            {
                                foreach (SqlParameter sqlp in sqlprms)
                                    cmd.Parameters.Add(sqlp);
                            }

                            await cmd.ExecuteNonQueryAsync();
                        }

                        await SqlTrans.CommitAsync();
                    }
                    catch (SqlException ae)
                    {
                        await SqlTrans.RollbackAsync();
                        strError.Error = true;
                        strError.ErrorList.Add(new ErrorList() { ErrorCode = ae.ErrorCode, Message = ae.Message, ErrorType = "SQL", WarningLevel = Level.Error });

                        //_logs.Write("DatabaseObject", "Execute", ae);
                    }
                    catch (Exception ae)
                    {
                        await SqlTrans.RollbackAsync();
                        strError.Error = true;
                        strError.ErrorList.Add(new ErrorList() { ErrorCode = 999, Message = ae.Message, ErrorType = "General", WarningLevel = Level.Error });

                        //_logs.Write("DatabaseObject", "Execute", ae);
                    }
                }
            }

            return strError;
        }

        public async Task<(DataTableCollection tables, ErrorResponse error)> Fetch(string query, SqlParameter[] sqlprms)
        {
            return await Fetch(query, sqlprms, CommandType.Text);
        }

        public async Task<(DataTableCollection tables, ErrorResponse error)> Fetch(string query, SqlParameter[] sqlprms, CommandType commandType)
        {
            ErrorResponse strError = new ErrorResponse();

            using (SqlConnection SqlCon = new SqlConnection(_connectionString))
            {
                if (SqlCon.State == ConnectionState.Open)
                {
                    await SqlCon.CloseAsync();
                }

                await SqlCon.OpenAsync();
                try
                {
                    DataSet ds = new DataSet("DataSet1");
                    using (SqlCommand cmd = new SqlCommand(query, SqlCon))
                    {
                        cmd.CommandType = commandType;
                        if (sqlprms != null)
                        {
                            foreach (SqlParameter sqlp in sqlprms)
                                cmd.Parameters.Add(sqlp);
                        }

                        using (SqlDataAdapter sdapt = new SqlDataAdapter(cmd))
                        {
                            sdapt.Fill(ds);
                        }
                    }

                    return (ds.Tables, strError);
                }
                catch (SqlException ae)
                {
                    strError.Error = true;
                    strError.ErrorList.Add(new ErrorList() { ErrorCode = ae.ErrorCode, Message = ae.Message, ErrorType = "SQL", WarningLevel = Level.Error });

                    //_logs.Write("DatabaseObject", "Fetch", ae);

                    return (null, strError);
                }
                catch (Exception ae)
                {
                    strError.Error = true;
                    strError.ErrorList.Add(new ErrorList() { ErrorCode = 999, Message = ae.Message, ErrorType = "General", WarningLevel = Level.Error });

                    //_logs.Write("DatabaseObject", "Fetch", ae);

                    return (null, strError);
                }
            }
        }

        public async Task<(DataTable table, ErrorResponse error)> ExecuteAndFetchTable(string query, SqlParameter[] sqlprms)
        {
            return await ExecuteAndFetchTable(query, sqlprms, CommandType.Text);
        }

        public async Task<(DataTable table, ErrorResponse error)> ExecuteAndFetchTable(string query, SqlParameter[] sqlprms, CommandType commandType)
        {
            ErrorResponse strError = new ErrorResponse();

            using (SqlConnection SqlCon = new SqlConnection(_connectionString))
            {
                if (SqlCon.State == ConnectionState.Open)
                {
                    await SqlCon.CloseAsync();
                }

                await SqlCon.OpenAsync();

                using (SqlTransaction SqlTrans = SqlCon.BeginTransaction())
                {
                    try
                    {
                        DataTable dt = new DataTable("DataTable1");
                        using (SqlCommand cmd = new SqlCommand(query, SqlCon))
                        {
                            cmd.CommandType = commandType;
                            cmd.Transaction = SqlTrans;
                            if (sqlprms != null)
                            {
                                foreach (SqlParameter sqlp in sqlprms)
                                    cmd.Parameters.Add(sqlp);
                            }

                            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                            {
                                dt.Load(reader);
                            }
                        }

                        await SqlTrans.CommitAsync();
                        return (dt, strError);
                    }
                    catch (SqlException ae)
                    {
                        await SqlTrans.RollbackAsync();
                        strError.Error = true;
                        strError.ErrorList.Add(new ErrorList() { ErrorCode = ae.ErrorCode, Message = ae.Message, ErrorType = "SQL", WarningLevel = Level.Error });

                        //_logs.Write("DatabaseObject", "ExecuteAndFetchTable", ae);

                        return (null, strError);
                    }
                    catch (Exception ae)
                    {
                        await SqlTrans.RollbackAsync();
                        strError.Error = true;
                        strError.ErrorList.Add(new ErrorList() { ErrorCode = 999, Message = ae.Message, ErrorType = "General", WarningLevel = Level.Error });

                        //_logs.Write("DatabaseObject", "ExecuteAndFetch", ae);

                        return (null, strError);
                    }
                }
            }
        }

        public async Task<(DataTable table, ErrorResponse error)> FetchTable(string query, SqlParameter[] sqlprms)
        {
            return await FetchTable(query, sqlprms, CommandType.Text);
        }

        public async Task<(DataTable table, ErrorResponse error)> FetchTable(string query, SqlParameter[] sqlprms, CommandType commandType)
        {
            ErrorResponse strError = new ErrorResponse();

            using (SqlConnection SqlCon = new SqlConnection(_connectionString))
            {
                if (SqlCon.State == ConnectionState.Open)
                {
                    await SqlCon.CloseAsync();
                }

                await SqlCon.OpenAsync();
                try
                {
                    DataTable dt = new DataTable("Table1");
                    using (SqlCommand cmd = new SqlCommand(query, SqlCon))
                    {
                        cmd.CommandType = commandType;
                        if (sqlprms != null)
                        {
                            foreach (SqlParameter sqlp in sqlprms)
                                cmd.Parameters.Add(sqlp);
                        }

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (!reader.IsClosed && reader.HasRows)
                            {
                                // Explicitly load the result set
                                dt.Load(reader);
                            }

                        }
                    }

                    return (dt, strError);
                }
                catch (SqlException ae)
                {
                    strError.Error = true;
                    strError.ErrorList.Add(new ErrorList() { ErrorCode = ae.ErrorCode, Message = ae.Message, ErrorType = "SQL", WarningLevel = Level.Error });

                    //_logs.Write("DatabaseObject", "Fetch", ae);

                    return (null, strError);
                }
                catch (Exception ae)
                {
                    strError.Error = true;
                    strError.ErrorList.Add(new ErrorList() { ErrorCode = 999, Message = ae.Message, ErrorType = "General", WarningLevel = Level.Error });

                    //_logs.Write("DatabaseObject", "Fetch", ae);

                    return (null, strError);
                }
            }
        }

        public async Task<string> DLookup(string query)
        {
            var result = await DLookup(query, null, CommandType.Text);
            return result.value;
        }

        public async Task<(string value, ErrorResponse error)> DLookup_response(string query)
        {
            var result = await DLookup(query, null, CommandType.Text);
            return result;
        }

        public async Task<(string value, ErrorResponse error)> DLookup(string query, SqlParameter[] sqlprms)
        {
            return await DLookup(query, sqlprms, CommandType.Text);
        }

        public async Task<(string value, ErrorResponse error)> DLookup(string query, SqlParameter[] sqlprms, CommandType commandType)
        {
            string Val = string.Empty;
            ErrorResponse strError = new ErrorResponse();

            using (SqlConnection SqlCon = new SqlConnection(_connectionString))
            {
                SqlCon.Open();
                try
                {
                    DataTable dt = new DataTable();
                    using (SqlCommand cmd = SqlCon.CreateCommand())
                    {
                        cmd.CommandText = query;
                        cmd.CommandType = commandType;
                        if (sqlprms != null)
                        {
                            foreach (SqlParameter param in sqlprms)
                                cmd.Parameters.Add(param);
                        }

                        object? v = await cmd.ExecuteScalarAsync();
                        Val = DataHelper.stringParse(v);
                    }
                }
                catch (SqlException ae)
                {
                    strError.Error = true;
                    strError.ErrorList.Add(new ErrorList() { ErrorCode = ae.ErrorCode, Message = ae.Message, ErrorType = "SQL", WarningLevel = Level.Error });

                    //_logs.Write("DatabaseObject", "DLookup(" + query + ")", ae);
                }
                catch (Exception ae)
                {
                    strError.Error = true;
                    strError.ErrorList.Add(new ErrorList() { ErrorCode = 999, Message = ae.Message, ErrorType = "General", WarningLevel = Level.Error });

                    //_logs.Write("DatabaseObject", "DLookup(" + query + ")", ae);
                }
            }

            return (Val, strError);
        }

        #region Bulk Transaction

        public string GetConnectionString()
        {
            return _connectionString;
        }

        public async Task<(DataTableCollection tables, ErrorResponse error)> ExecuteAndFetch(string query, SqlParameter[] sqlprms, CommandType commandType, SqlConnection sqlCon, SqlTransaction sqlTrans)
        {
            ErrorResponse strError = new ErrorResponse();
            try
            {
                DataSet ds = new DataSet("DataSet1");
                using (SqlCommand cmd = new SqlCommand(query, sqlCon))
                {
                    cmd.CommandType = commandType;
                    cmd.Transaction = sqlTrans;
                    if (sqlprms != null)
                    {
                        foreach (SqlParameter sqlp in sqlprms)
                            cmd.Parameters.Add(sqlp);
                    }

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        do
                        {
                            DataTable dt = new DataTable();
                            dt.Load(reader);
                            ds.Tables.Add(dt);
                        }
                        while (await reader.NextResultAsync());
                    }
                }
                return (ds.Tables, strError);
            }
            catch (SqlException ae)
            {
                strError.Error = true;
                strError.ErrorList.Add(new ErrorList() { ErrorCode = ae.ErrorCode, Message = ae.Message, ErrorType = "SQL", WarningLevel = Level.Error });

                //_logs.Write("DatabaseObject", "ExecuteAndFetch", ae);
            }
            catch (Exception ae)
            {
                strError.Error = true;
                strError.ErrorList.Add(new ErrorList() { ErrorCode = 999, Message = ae.Message, ErrorType = "General", WarningLevel = Level.Error });

                //_logs.Write("DatabaseObject", "ExecuteAndFetch", ae);
            }

            return (null, strError);
        }

        public async Task<ErrorResponse> Execute(string query, SqlParameter[] sqlprms, CommandType commandType, SqlConnection sqlCon, SqlTransaction sqlTrans)
        {
            ErrorResponse strError = new ErrorResponse();
            try
            {
                DataSet ds = new DataSet("DataSet1");
                using (SqlCommand cmd = new SqlCommand(query, sqlCon))
                {
                    cmd.CommandType = commandType;
                    cmd.Transaction = sqlTrans;
                    if (sqlprms != null)
                    {
                        foreach (SqlParameter sqlp in sqlprms)
                            cmd.Parameters.Add(sqlp);
                    }

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ae)
            {
                strError.Error = true;
                strError.ErrorList.Add(new ErrorList() { ErrorCode = ae.ErrorCode, Message = ae.Message, ErrorType = "SQL", WarningLevel = Level.Error });

                //_logs.Write("DatabaseObject", "Execute", ae);
            }
            catch (Exception ae)
            {
                strError.Error = true;
                strError.ErrorList.Add(new ErrorList() { ErrorCode = 999, Message = ae.Message, ErrorType = "General", WarningLevel = Level.Error });

                //_logs.Write("DatabaseObject", "Execute", ae);
            }

            return strError;
        }

        public async Task<(DataTableCollection tables, ErrorResponse error)> Fetch(string query, SqlParameter[] sqlprms, CommandType commandType, SqlConnection sqlCon, SqlTransaction sqlTrans)
        {
            ErrorResponse strError = new ErrorResponse();

            try
            {
                DataSet ds = new DataSet("DataSet1");
                using (SqlCommand cmd = new SqlCommand(query, sqlCon, sqlTrans))
                {
                    cmd.CommandType = commandType;
                    if (sqlprms != null)
                    {
                        foreach (SqlParameter sqlp in sqlprms)
                            cmd.Parameters.Add(sqlp);
                    }

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        do
                        {
                            DataTable dt = new DataTable();
                            dt.Load(reader);
                            ds.Tables.Add(dt);
                        }
                        while (await reader.NextResultAsync());
                    }
                }

                return (ds.Tables, strError);
            }
            catch (SqlException ae)
            {
                strError.Error = true;
                strError.ErrorList.Add(new ErrorList() { ErrorCode = ae.ErrorCode, Message = ae.Message, ErrorType = "SQL", WarningLevel = Level.Error });

                //_logs.Write("DatabaseObject", "Fetch", ae);
            }
            catch (Exception ae)
            {
                strError.Error = true;
                strError.ErrorList.Add(new ErrorList() { ErrorCode = 999, Message = ae.Message, ErrorType = "General", WarningLevel = Level.Error });

                //_logs.Write("DatabaseObject", "Fetch", ae);
            }

            return (null, strError);
        }

        public async Task<(DataTable table, ErrorResponse error)> ExecuteAndFetchTable(string query, SqlParameter[] sqlprms, CommandType commandType, SqlConnection sqlCon, SqlTransaction sqlTrans)
        {
            ErrorResponse strError = new ErrorResponse();
            try
            {
                DataTable dt = new DataTable("DataTable1");
                using (SqlCommand cmd = new SqlCommand(query, sqlCon, sqlTrans))
                {
                    cmd.CommandType = commandType;
                    if (sqlprms != null)
                    {
                        foreach (SqlParameter sqlp in sqlprms)
                            cmd.Parameters.Add(sqlp);
                    }

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        dt.Load(reader);
                    }
                }

                return (dt, strError);
            }
            catch (SqlException ae)
            {
                strError.Error = true;
                strError.ErrorList.Add(new ErrorList() { ErrorCode = ae.ErrorCode, Message = ae.Message, ErrorType = "SQL", WarningLevel = Level.Error });

                //_logs.Write("DatabaseObject", "ExecuteAndFetchTable", ae);
            }
            catch (Exception ae)
            {
                strError.Error = true;
                strError.ErrorList.Add(new ErrorList() { ErrorCode = 999, Message = ae.Message, ErrorType = "General", WarningLevel = Level.Error });

                //_logs.Write("DatabaseObject", "ExecuteAndFetch", ae);
            }

            return (null, strError);
        }

        public async Task<(DataTable table, ErrorResponse error)> FetchTable(string query, SqlParameter[] sqlprms, CommandType commandType, SqlConnection sqlCon, SqlTransaction sqlTrans)
        {
            var result = await Fetch(query, sqlprms, commandType, sqlCon, sqlTrans);
            if (result.error.Error)
            {
                return (null, result.error);
            }
            else
            {
                return (result.tables[0], result.error);
            }
        }

        public async Task<(string value, ErrorResponse error)> DLookup(string query, SqlParameter[] sqlprms, CommandType commandType, SqlConnection sqlCon, SqlTransaction sqlTrans)
        {
            string Val = string.Empty;
            ErrorResponse strError = new ErrorResponse();

            try
            {
                DataTable dt = new DataTable();
                using (SqlCommand cmd = sqlCon.CreateCommand())
                {
                    cmd.Transaction = sqlTrans;
                    cmd.CommandText = query;
                    cmd.CommandType = commandType;
                    if (sqlprms != null)
                    {
                        foreach (SqlParameter param in sqlprms)
                            cmd.Parameters.Add(param);
                    }

                    object? value = await cmd.ExecuteScalarAsync();
                    Val = DataHelper.stringParse(value);
                }

            }
            catch (SqlException ae)
            {
                strError.Error = true;
                strError.ErrorList.Add(new ErrorList() { ErrorCode = ae.ErrorCode, Message = ae.Message, ErrorType = "SQL", WarningLevel = Level.Error });

                //_logs.Write("DatabaseObject", "DLookup(" + query + ")", ae);
            }
            catch (Exception ae)
            {
                strError.Error = true;
                strError.ErrorList.Add(new ErrorList() { ErrorCode = 999, Message = ae.Message, ErrorType = "General", WarningLevel = Level.Error });

                //_logs.Write("DatabaseObject", "DLookup(" + query + ")", ae);
            }

            return (Val, strError);
        }

        #endregion Bulk Transaction
    }
}
