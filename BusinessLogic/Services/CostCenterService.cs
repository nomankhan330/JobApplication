using Azure.Core;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using BusinessLogic.Helper;
using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using static System.Reflection.Metadata.BlobBuilder;

namespace BusinessLogic.Services
{
    public class CostCenterService : ICostCenter
    {
        private readonly AppDbContext _context;
        private readonly ISessionHelper _session;
        private readonly ILogs _logs;
        private readonly IDatabaseObject _db;

        public CostCenterService(AppDbContext context, ISessionHelper session, ILogs logs, IDatabaseObject db)
        {
            _context = context;
            _session = session;
            _logs = logs;
            _db = db;
        }

        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public async Task<dynamic> SaveCostCenter(CostCenter model)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    if (model.Id == 0)
                    {
                        // Duplicate check (CostCenterCode)
                        var exists = _context.CostCenters
                                             .Any(c => c.CostCenterName.ToLower() == model.CostCenterName.ToLower() && c.ReferenceId == _session.ReferenceId);

                        if (exists)
                        {
                            return new
                            {
                                errorCode = 201,
                                data = "Duplicate Cost Center Name"
                            };
                        }

                        var maxId = await _context.CostCenters.MaxAsync(c => (int?)c.Id) ?? 0;
                        int newId = maxId + 1;

                        var costCenter = new CostCenter
                        {
                            Id = newId,
                            CostCenterCode = $"CC-{newId.ToString("D6")}",
                            CostCenterName = model.CostCenterName,
                            Address = model.Address,
                            ContactPerson = model.ContactPerson,
                            ContactNumber = model.ContactNumber,
                            Cnic = model.Cnic,
                            Photo = model.Photo,
                            Notes = model.Notes,
                            AdditionalFields = model.AdditionalFields,
                            MyPercentage = model.MyPercentage,
                            IsActive = model.IsActive,
                            ReferenceId = _session.ReferenceId,
                            CreatedBy = _session.LoginId,
                            CreatedOn = DateTime.Now
                        };

                        _context.CostCenters.Add(costCenter);
                    }
                    else
                    {
                        var existingRecord = await _context.CostCenters.FindAsync(model.Id);

                        if (existingRecord == null)
                        {
                            return new
                            {
                                errorCode = 404,
                                errorMessage = "Cost Center not found"
                            };
                        }

                        // 🔹 Duplicate check (exclude current record)
                        var exists = await _context.CostCenters
                            .AnyAsync(c => c.CostCenterName.ToLower() == model.CostCenterName.ToLower()
                                        && c.Id != model.Id && c.ReferenceId == _session.ReferenceId);

                        if (exists)
                        {
                            return new
                            {
                                errorCode = 201,
                                data = "Duplicate Cost Center Name"
                            };
                        }

                        // Update fields
                        existingRecord.CostCenterName = model.CostCenterName;
                        //existingRecord.CostCenterCode = model.CostCenterCode;
                        existingRecord.Address = model.Address;
                        existingRecord.ContactPerson = model.ContactPerson;
                        existingRecord.ContactNumber = model.ContactNumber;
                        existingRecord.Cnic = model.Cnic;
                        existingRecord.Notes = model.Notes;
                        existingRecord.AdditionalFields = model.AdditionalFields;
                        existingRecord.MyPercentage = model.MyPercentage;
                        existingRecord.IsActive = model.IsActive;

                        if (!string.IsNullOrEmpty(model.Photo))
                        {
                            existingRecord.Photo = model.Photo;
                        }

                        existingRecord.ModifiedBy = _session.LoginId;
                        existingRecord.ModifiedOn = DateTime.Now;
                    }

                    await _context.SaveChangesAsync();
                    transaction.Commit();

                    return new
                    {
                        errorCode = 200,
                        data = "Cost Center saved successfully"
                    };
                }
                catch (Exception ae)
                {
                    transaction.Rollback();

                    _logs.Write(
                        "Account",
                        "SaveCostCenter",
                        ae.InnerException?.Message ?? ae.Message
                    );

                    return new
                    {
                        errorCode = 999,
                        errorMessage = ae.Message
                    };
                }
            }
        }

        public async Task<dynamic> GetCostCenters(IList<QueryFilters> filters)
        {
            try
            {
                int draw = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "draw")?.filterValue ?? "0");
                int start = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "start")?.filterValue ?? "0");
                int length = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "length")?.filterValue ?? "10");

                string search = filters.FirstOrDefault(x => x.fieldName == "search")?.filterValue;

                string name = filters.FirstOrDefault(x => x.fieldName == "CostCenterName")?.filterValue;
                string code = filters.FirstOrDefault(x => x.fieldName == "CostCenterCode")?.filterValue;
                string contact = filters.FirstOrDefault(x => x.fieldName == "ContactNumber")?.filterValue;
                string status = filters.FirstOrDefault(x => x.fieldName == "IsActive")?.filterValue;

                int orderColumnIndex = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "orderColumn")?.filterValue ?? "0");
                string orderDir = filters.FirstOrDefault(x => x.fieldName == "orderDir")?.filterValue ?? "desc";

                var columns = new[]
                {
                    "Id",
                    "CostCenterName",
                    "CostCenterCode",
                    "ContactPerson",
                    "CreatedOn",
                    "IsActive"
                };

                //var query = null;

                var query =
                    from c in _context.CostCenters
                    join u in _context.Users on c.CreatedBy equals u.Id into userJoin
                    from user in userJoin.DefaultIfEmpty()
                    select new
                    {
                        c.Id,
                        c.CostCenterName,
                        c.CostCenterCode,
                        c.ContactPerson,
                        c.ContactNumber,
                        CreatedByName = user != null ? user.UserName : "",
                        ReferenceId = c.ReferenceId,
                        CreatedBy = c.CreatedBy,
                        c.CreatedOn,
                        c.IsActive
                    };

                // User Type Wise Filter
                if (_session.UserType == 2)
                {
                    // Admin / Company User
                    query = query.Where(x => x.ReferenceId == _session.ReferenceId);
                }
                else
                {
                    // Normal User
                    query = query.Where(x =>
                        _context.UserCostCenters.Any(uc =>
                            uc.UserId == _session.LoginId &&
                            uc.CostCenterId == x.Id));
                }

                // 🔍 Global Search
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(x =>
                        x.CostCenterName.Contains(search) ||
                        x.CostCenterCode.Contains(search) ||
                        x.ContactPerson.Contains(search)
                    );
                }

                // 🔍 Filters
                if (!string.IsNullOrEmpty(name))
                    query = query.Where(x => x.CostCenterName.Contains(name));

                if (!string.IsNullOrEmpty(code))
                    query = query.Where(x => x.CostCenterCode.Contains(code));

                if (!string.IsNullOrEmpty(contact))
                    query = query.Where(x => x.ContactPerson.Contains(contact));

                if (!string.IsNullOrEmpty(status))
                {
                    bool isActive = Convert.ToBoolean(status);
                    query = query.Where(x => x.IsActive == isActive);
                }

                int totalRecords = await query.CountAsync();

                // 🔥 Sorting
                if (orderColumnIndex >= 0 && orderColumnIndex < columns.Length)
                {
                    string sortColumn = columns[orderColumnIndex];

                    query = orderDir == "asc"
                        ? query.OrderBy(x => EF.Property<object>(x, sortColumn))
                        : query.OrderByDescending(x => EF.Property<object>(x, sortColumn));
                }
                else
                {
                    query = query.OrderByDescending(x => x.Id);
                }

                var data = await query
                    .Skip(start)
                    .Take(length)
                    .ToListAsync();

                return new
                {
                    draw,
                    recordsTotal = totalRecords,
                    recordsFiltered = totalRecords,
                    data,
                    errorCode = 200
                };
            }
            catch (Exception ex)
            {
                _logs.Write("CostCenter", "GetCostCenters", ex.Message);

                return new
                {
                    errorCode = 999,
                    errorMessage = ex.Message
                };
            }
        }

        //public async Task<dynamic> GetCostCenters(IList<QueryFilters> filters)
        //{
        //    try
        //    {
        //        var result = await (
        //            from c in _context.CostCenters

        //                // join CreatedBy user
        //            join u in _context.Users on c.CreatedBy equals u.Id into userJoin
        //            from user in userJoin.DefaultIfEmpty()

        //            select new
        //            {
        //                c.Id,
        //                c.CostCenterName,
        //                c.CostCenterCode,
        //                c.ContactPerson,
        //                c.ContactNumber,
        //                CreatedBy = user.UserName,
        //                c.CreatedOn,
        //                c.IsActive                    }
        //        ).ToListAsync();

        //        return new
        //        {
        //            errorCode = 200,
        //            data = result
        //        };
        //    }
        //    catch (Exception ae)
        //    {
        //        _logs.Write(
        //            "Account",
        //            "GetCostCenters",
        //            ae.InnerException?.Message ?? ae.Message
        //        );

        //        return new
        //        {
        //            errorCode = 999,
        //            errorMessage = ae.Message
        //        };
        //    }
        //}

        public async Task<dynamic> GetCostCenterById(int id)
        {
            try
            {
                var costCenter = await _context.CostCenters
                                               .FirstOrDefaultAsync(c => c.Id == id);

                if (costCenter == null)
                {
                    return new
                    {
                        errorCode = 404,
                        errorMessage = "Cost Center not found"
                    };
                }

                return new
                {
                    errorCode = 200,
                    CostCenter = new
                    {
                        costCenter.Id,
                        costCenter.CostCenterName,
                        costCenter.CostCenterCode,
                        costCenter.Address,
                        costCenter.ContactPerson,
                        costCenter.ContactNumber,
                        costCenter.Cnic,
                        costCenter.Photo,
                        costCenter.Notes,
                        costCenter.AdditionalFields,
                        costCenter.MyPercentage,
                        costCenter.IsActive
                    }
                };
            }
            catch (Exception ae)
            {
                _logs.Write(
                    "Account",
                    "GetCostCenterById",
                    ae.InnerException?.Message ?? ae.Message
                );

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> DeleteCostCenterById(int id)
        {
            try
            {
                if (id == 0)
                {
                    return new
                    {
                        errorMessage = "Invalid Id"
                    };
                }

                var existingRecord = await _context.CostCenters
                                                   .FirstOrDefaultAsync(c => c.Id == id);

                if (existingRecord == null)
                {
                    return new
                    {
                        errorMessage = "Cost Center not found"
                    };
                }

                _context.CostCenters.Remove(existingRecord);
                await _context.SaveChangesAsync();

                return new
                {
                    errorCode = 200,
                    message = "Cost Center deleted successfully"
                };
            }
            catch (Exception ae)
            {
                _logs.Write(
                    "Account",
                    "DeleteCostCenterById",
                    ae.InnerException?.Message ?? ae.Message
                );

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> ToggleCostCenterStatus(int id)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    if (id == 0)
                    {
                        return new
                        {
                            errorCode = 400,
                            errorMessage = "Invalid Id"
                        };
                    }

                    var record = await _context.CostCenters
                                               .FirstOrDefaultAsync(c => c.Id == id);

                    if (record == null)
                    {
                        return new
                        {
                            errorCode = 404,
                            errorMessage = "Cost Center not found"
                        };
                    }

                    // 🔥 Toggle Logic
                    record.IsActive = !(record.IsActive ?? false);

                    record.ModifiedBy = _session.LoginId;
                    record.ModifiedOn = DateTime.Now;

                    await _context.SaveChangesAsync();
                    transaction.Commit();

                    return new
                    {
                        errorCode = 200,
                        message = "Cost Center status updated successfully",
                        isActive = record.IsActive
                    };
                }
                catch (Exception ex)
                {
                    transaction.Rollback();

                    _logs.Write(
                        "CostCenter",
                        "ToggleCostCenterStatus",
                        ex.InnerException?.Message ?? ex.Message
                    );

                    return new
                    {
                        errorCode = 999,
                        errorMessage = ex.Message
                    };
                }
            }
        }

        public async Task<dynamic> GetCostCenterReport_Bk(IList<QueryFilters> filters)
        {
            int draw = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "draw")?.filterValue ?? "0");
            int start = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "start")?.filterValue ?? "0");
            int length = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "length")?.filterValue ?? "10");

            string costCenterId = filters.FirstOrDefault(x => x.fieldName == "costCenterId")?.filterValue;
            string fromDate = filters.FirstOrDefault(x => x.fieldName == "fromDate")?.filterValue;
            string toDate = filters.FirstOrDefault(x => x.fieldName == "toDate")?.filterValue;
            string status = filters.FirstOrDefault(x => x.fieldName == "status")?.filterValue;

            var grossProfitCalc = 0;
            var myPercentageCalc = 0;

            var query =

                from inv in _context.SalesInvoices

                join jm in _context.JobImportMasters
                    on inv.JobId equals jm.Id

                join cc in _context.CostCenters
                    on jm.CostCenterId equals cc.Id into ccJoin
                from cc in ccJoin.DefaultIfEmpty()

                join c in _context.Customers
                    on jm.CustomerId equals c.Id into custJoin
                from c in custJoin.DefaultIfEmpty()

                join mos in _context.ShipmentModes
                    on jm.ShipmentModeId equals mos.Id into mosJoin
                from mos in mosJoin.DefaultIfEmpty()

                select new
                {
                    Id = inv.InvoiceId,

                    CostCenterId = cc.Id,

                    CostCenter = cc.CostCenterName,
                    
                    Percentage = cc.MyPercentage,

                    JobDate = jm.JobDate,

                    JobNo = jm.JobNumber,

                    ModeOfShipment = mos.Name,

                    ShipmentTypeName =
                        jm.ShipmentType == 1 ? "Import" : "Export",

                    CustomerName = c.CustomerName,

                    BLNumber = jm.BlNo,

                    InvoiceDate = inv.InvoiceDate,

                    InvoiceNo = inv.InvoiceNo,

                    InvoiceAmount = inv.TotalAmount,

                    TotalCost = _context.JobImportPayments
                        .Where(x => x.JobImportMasterId == jm.Id)
                        .Sum(x => (decimal?)x.Amount) ?? 0,

                    VATAmount = _context.SalesInvoiceDetails
                        .Where(x => x.InvoiceId == inv.InvoiceId)
                        .Sum(x => (decimal?)x.VatAmount) ?? 0,


                    //Total Amount - Total Cost - VAT Amount
                    grossProfitCalc =
                        ( inv.TotalAmount - 
                        
                        (_context.JobImportPayments
                            .Where(x => x.JobImportMasterId == jm.Id)
                            .Sum(x => (decimal?)x.Amount) ?? 0)

                        -

                        (_context.SalesInvoiceDetails
                        .Where(x => x.InvoiceId == inv.InvoiceId)
                        .Sum(x => (decimal?)x.VatAmount) ?? 0)
                        ),

                    GrossProfit = grossProfitCalc,

                    LossAmount = grossProfitCalc < 0 ? Math.Abs(grossProfitCalc) : 0,

                    myPercentageCalc = ((grossProfitCalc * cc.MyPercentage) / 100),
                    
                    MyPercentage = myPercentageCalc,

                    //NetProfit = grossProfit - myPercentage,

                    NetProfit = grossProfitCalc > 0 ? (grossProfitCalc - myPercentageCalc) : 0,

                    Remarks = jm.Remarks,

                    Status = inv.Status
                };

            // FILTERS


            if (!string.IsNullOrEmpty(costCenterId))
            {
                int costCenter = Convert.ToInt32(costCenterId);
                query = query.Where(x => x.CostCenterId == costCenter);
            }


            if (!string.IsNullOrEmpty(fromDate))
            {
                DateOnly from = DateOnly.Parse(fromDate);

                query = query.Where(x => x.JobDate >= from);
            }

            if (!string.IsNullOrEmpty(toDate))
            {
                DateOnly to = DateOnly.Parse(toDate);

                query = query.Where(x => x.JobDate <= to);
            }

            if (!string.IsNullOrEmpty(status))
            {
                if (int.TryParse(status, out int statusInt))
                {
                    query = query.Where(x => x.Status == statusInt);
                }
            }

            int totalRecords = await query.CountAsync();

            var data = await query
                .OrderByDescending(x => x.Id)
                .Skip(start)
                .Take(length)
                .ToListAsync();

            return new
            {
                draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecords,
                data,
                errorCode = 200
            };
        }

        public async Task<dynamic> GetCostCenterReport(IList<QueryFilters> filters)
        {
            int draw = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "draw")?.filterValue ?? "0");
            int start = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "start")?.filterValue ?? "0");
            int length = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "length")?.filterValue ?? "10");

            string costCenterId = filters.FirstOrDefault(x => x.fieldName == "costCenterId")?.filterValue;
            string fromDate = filters.FirstOrDefault(x => x.fieldName == "fromDate")?.filterValue;
            string toDate = filters.FirstOrDefault(x => x.fieldName == "toDate")?.filterValue;
            string status = filters.FirstOrDefault(x => x.fieldName == "status")?.filterValue;

            string orderColumn = filters.FirstOrDefault(x => x.fieldName == "orderColumn")?.filterValue;
            string orderDir = filters.FirstOrDefault(x => x.fieldName == "orderDir")?.filterValue;

            SqlParameter[] parameters =
            {
                new SqlParameter("@CostCenterId", string.IsNullOrEmpty(costCenterId) ? DBNull.Value : costCenterId),
                new SqlParameter("@FromDate", string.IsNullOrEmpty(fromDate) ? DBNull.Value : fromDate),
                new SqlParameter("@ToDate", string.IsNullOrEmpty(toDate) ? DBNull.Value : toDate),
                new SqlParameter("@Status", string.IsNullOrEmpty(status) ? DBNull.Value : status),
                new SqlParameter("@OrderColumn", orderColumn ?? "Id"),
                new SqlParameter("@OrderDir", orderDir ?? "desc")
            };

            var result = await _db.Fetch(
                "sp_GetCostCenterReport",
                parameters,
                CommandType.StoredProcedure
            );

            var table = result.tables[0];

            int totalRecords = table.Rows.Count;

            // 🔥 SAFE CONVERSION (IMPORTANT FIX)
            var data = table.AsEnumerable()
                .Skip(start)
                .Take(length)
                .Select(row => table.Columns.Cast<DataColumn>()
                    .ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

            return new
            {
                draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecords,
                data,
                errorCode = 200
            };
        }

        
    }
}
