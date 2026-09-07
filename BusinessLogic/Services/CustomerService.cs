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
    public class CustomerService : ICustomer
    {
        private readonly AppDbContext _context;
        private readonly ISessionHelper _session;
        private readonly ILogs _logs;
        private readonly IDatabaseObject _db;

        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public CustomerService(AppDbContext context, ISessionHelper session, ILogs logs, IDatabaseObject db)
        {
            _context = context;
            _session = session;
            _logs = logs;
            _db = db;
        }

        public async Task<dynamic> Save(Customer model)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    if (model.Id == 0)
                    {
                        // 🔹 Duplicate check (CustomerName)
                        var exists = await _context.Customers
                            .AnyAsync(c => c.CustomerName.ToLower() == model.CustomerName.ToLower() && c.ReferenceId == _session.ReferenceId);

                        if (exists)
                        {
                            return new
                            {
                                errorCode = 201,
                                data = "Duplicate Customer Name"
                            };
                        }

                        var maxId = await _context.Customers.MaxAsync(c => (int?)c.Id) ?? 0;
                        int newId = maxId + 1;

                        string prefix = model.TypeId == 2 ? "SPV" : "CUS";

                        var customer = new Customer
                        {
                            Id = newId,
                            CustomerCode = $"{prefix}-{newId.ToString("D6")}",
                            TypeId = model.TypeId,
                            Language = model.Language,
                            CompanyId = model.CompanyId,
                            CustomerName = model.CustomerName,
                            CustomerNameAr = model.CustomerNameAr,
                            MailingName = model.MailingName,
                            MailingNameAr = model.MailingNameAr,
                            Email = model.Email,
                            ContactNo = model.ContactNo,
                            Address = model.Address,
                            AddressAr = model.AddressAr,
                            VatId = model.VatId,
                            VatDeduction = model.VatDeduction,
                            VatRate = model.VatRate,
                            RegionId = model.RegionId,
                            CountryId = model.CountryId,
                            Pobox = model.Pobox,
                            LocalLanguageMailing = model.LocalLanguageMailing,
                            IsActive = model.IsActive,
                            BankName = model.BankName,
                            AccountNumber = model.AccountNumber,
                            Iban = model.Iban,
                            SwiftCode = model.SwiftCode,
                            ReferenceId = _session.ReferenceId,
                            CreatedBy = _session.LoginId,
                            CreatedOn = DateTime.Now
                        };

                        _context.Customers.Add(customer);
                    }
                    else
                    {
                        var existingRecord = await _context.Customers.FindAsync(model.Id);

                        if (existingRecord == null)
                        {
                            return new
                            {
                                errorCode = 404,
                                data = "Customer not found"
                            };
                        }

                        // 🔹 Duplicate check (exclude current record)
                        var exists = await _context.Customers
                            .AnyAsync(c => c.CustomerName.ToLower() == model.CustomerName.ToLower()
                                        && c.Id != model.Id && c.ReferenceId == _session.ReferenceId);

                        if (exists)
                        {
                            return new
                            {
                                errorCode = 201,
                                data = "Duplicate Customer Name"
                            };
                        }

                        string prefix = model.TypeId == 2 ? "SPV" : "CUS";

                        // 🔹 Update fields
                        existingRecord.CustomerCode = $"{prefix}-{model.Id.ToString("D6")}";
                        existingRecord.TypeId = model.TypeId;
                        existingRecord.Language = model.Language;
                        existingRecord.CompanyId = model.CompanyId;
                        existingRecord.CustomerName = model.CustomerName;
                        existingRecord.CustomerNameAr = model.CustomerNameAr;
                        existingRecord.MailingName = model.MailingName;
                        existingRecord.MailingNameAr = model.MailingNameAr;
                        existingRecord.Email = model.Email;
                        existingRecord.ContactNo = model.ContactNo;
                        existingRecord.Address = model.Address;
                        existingRecord.AddressAr = model.AddressAr;
                        existingRecord.VatId = model.VatId;
                        existingRecord.VatDeduction = model.VatDeduction;
                        existingRecord.VatRate = model.VatRate;
                        existingRecord.RegionId = model.RegionId;
                        existingRecord.CountryId = model.CountryId;
                        existingRecord.Pobox = model.Pobox;
                        existingRecord.LocalLanguageMailing = model.LocalLanguageMailing;
                        existingRecord.IsActive = model.IsActive;
                        existingRecord.BankName = model.BankName;
                        existingRecord.AccountNumber = model.AccountNumber;
                        existingRecord.Iban = model.Iban;
                        existingRecord.SwiftCode = model.SwiftCode;

                        existingRecord.ModifiedBy = _session.LoginId;
                        existingRecord.ModifiedOn = DateTime.Now;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new
                    {
                        errorCode = 200,
                        data = "Customer saved successfully"
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    _logs.Write(
                        "Customer",
                        "SaveCustomer",
                        ex.InnerException?.Message ?? ex.Message
                    );

                    return new
                    {
                        errorCode = 999,
                        data = ex.Message
                    };
                }
            }
        }

        public async Task<dynamic> GetCustomers(IList<QueryFilters> filters)
        {
            try
            {
                int draw = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "draw")?.filterValue ?? "0");
                int start = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "start")?.filterValue ?? "0");
                int length = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "length")?.filterValue ?? "10");
                string search = filters.FirstOrDefault(x => x.fieldName == "search")?.filterValue;

                string customerName = filters.FirstOrDefault(x => x.fieldName == "CustomerName")?.filterValue;
                string email = filters.FirstOrDefault(x => x.fieldName == "Email")?.filterValue;
                string vatId = filters.FirstOrDefault(x => x.fieldName == "VatId")?.filterValue;
                string status = filters.FirstOrDefault(x => x.fieldName == "IsActive")?.filterValue;

                // 🔥 SORTING PARAMS (NEW ADD)
                int orderColumnIndex = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "orderColumn")?.filterValue ?? "0");
                string orderDir = filters.FirstOrDefault(x => x.fieldName == "orderDir")?.filterValue ?? "asc";

                var columns = new[]
                {
                    "Id",
                    "CustomerCode",
                    "CustomerName",
                    "Email",
                    "VatId",
                    "CompanyName",
                    "CountryName",
                    "RegionName",
                    "IsActive",
                    "CreatedOn"
                };

                var query =
                    from c in _context.Customers

                    join u in _context.Users on c.CreatedBy equals u.Id into userJoin
                    from user in userJoin.DefaultIfEmpty()

                    join comp in _context.Companies on c.CompanyId equals comp.Id into compJoin
                    from company in compJoin.DefaultIfEmpty()

                    join co in _context.Countries on c.CountryId equals co.Id into countryJoin
                    from country in countryJoin.DefaultIfEmpty()

                    join r in _context.Regions on c.RegionId equals r.Id into regionJoin
                    from region in regionJoin.DefaultIfEmpty()

                    select new
                    {
                        c.Id,
                        c.CustomerCode,
                        c.CustomerName,
                        c.Email,
                        c.VatId,
                        c.Pobox,
                        Type = c.TypeId == 1 ? "Customer" : "Service Provider",
                        CompanyName = company.Name,
                        CountryName = country.Name,
                        RegionName = region.Name,
                        CreatedByName = user.UserName,
                        CreatedBy = c.CreatedBy,
                        ReferenceId = c.ReferenceId,
                        c.IsActive,
                        c.CreatedOn
                    };

                // 🔍 GLOBAL SEARCH
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(x =>
                        x.CustomerName.Contains(search) ||
                        x.Email.Contains(search) ||
                        x.VatId.Contains(search) ||
                        x.CustomerCode.Contains(search)
                    );
                }

                // Display only records created by the logged-in user
                if (_session.LoginType != 1)
                {
                    query = query.Where(x => x.ReferenceId == _session.ReferenceId);
                }

                // 🔍 FILTERS
                if (!string.IsNullOrEmpty(customerName))
                    query = query.Where(x => x.CustomerName.Contains(customerName));

                if (!string.IsNullOrEmpty(email))
                    query = query.Where(x => x.Email.Contains(email));

                if (!string.IsNullOrEmpty(vatId))
                    query = query.Where(x => x.VatId.Contains(vatId));

                if (!string.IsNullOrEmpty(status))
                {
                    bool isActive = Convert.ToBoolean(status);
                    query = query.Where(x => x.IsActive == isActive);
                }

                int totalRecords = await query.CountAsync();

                // 🔥 SORTING LOGIC (FIXED)
                if (orderColumnIndex >= 0 && orderColumnIndex < columns.Length)
                {
                    string sortColumn = columns[orderColumnIndex];

                    if (orderDir == "asc")
                    {
                        query = query.OrderBy(x => EF.Property<object>(x, sortColumn));
                    }
                    else
                    {
                        query = query.OrderByDescending(x => EF.Property<object>(x, sortColumn));
                    }
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
                _logs.Write("Customer", "GetCustomers", ex.InnerException?.Message ?? ex.Message);

                return new
                {
                    errorCode = 999,
                    errorMessage = ex.Message
                };
            }
        }

        //public async Task<dynamic> GetCustomers(IList<QueryFilters> filters)
        //{
        //    try
        //    {
        //        int draw = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "draw")?.filterValue ?? "0");
        //        int start = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "start")?.filterValue ?? "0");
        //        int length = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "length")?.filterValue ?? "10");
        //        string search = filters.FirstOrDefault(x => x.fieldName == "search")?.filterValue;

        //        string customerName = filters.FirstOrDefault(x => x.fieldName == "CustomerName")?.filterValue;
        //        string email = filters.FirstOrDefault(x => x.fieldName == "Email")?.filterValue;
        //        string vatId = filters.FirstOrDefault(x => x.fieldName == "VatId")?.filterValue;
        //        string status = filters.FirstOrDefault(x => x.fieldName == "IsActive")?.filterValue;

        //        var query =
        //            from c in _context.Customers

        //            // Users (CreatedBy)
        //            join u in _context.Users on c.CreatedBy equals u.Id into userJoin
        //            from user in userJoin.DefaultIfEmpty()

        //             // Company
        //            join comp in _context.Companies on c.CompanyId equals comp.Id into compJoin
        //            from company in compJoin.DefaultIfEmpty()

        //            // Country
        //            join co in _context.Countries on c.CountryId equals co.Id into countryJoin
        //            from country in countryJoin.DefaultIfEmpty()

        //             // Region
        //            join r in _context.Regions on c.RegionId equals r.Id into regionJoin
        //            from region in regionJoin.DefaultIfEmpty()

        //            select new
        //            {
        //                c.Id,
        //                c.CustomerCode,
        //                c.CustomerName,
        //                c.Email,
        //                c.VatId,

        //                //Relations
        //                CompanyName = company.Name,
        //                CountryName = country.Name,
        //                RegionName = region.Name,
        //                CreatedBy = user.UserName,

        //                c.IsActive,
        //                c.CreatedOn
        //            };

        //        // 🔍 GLOBAL SEARCH (FAST SEARCH BOX)
        //        if (!string.IsNullOrEmpty(search))
        //        {
        //            query = query.Where(x =>
        //                x.CustomerName.Contains(search) ||
        //                x.Email.Contains(search) ||
        //                x.VatId.Contains(search) ||
        //                x.CustomerCode.Contains(search)
        //            );
        //        }

        //        // 🔍 COLUMN FILTERS
        //        if (!string.IsNullOrEmpty(customerName))
        //            query = query.Where(x => x.CustomerName.Contains(customerName));

        //        if (!string.IsNullOrEmpty(email))
        //            query = query.Where(x => x.Email.Contains(email));

        //        if (!string.IsNullOrEmpty(vatId))
        //            query = query.Where(x => x.VatId.Contains(vatId));

        //        if (!string.IsNullOrEmpty(status))
        //        {
        //            bool isActive = Convert.ToBoolean(status);
        //            query = query.Where(x => x.IsActive == isActive);
        //        }

        //        int totalRecords = await query.CountAsync();

        //        var data = await query
        //            .OrderByDescending(x => x.Id)
        //            .Skip(start)
        //            .Take(length)
        //            .ToListAsync();

        //        return new
        //        {
        //            draw = draw,
        //            recordsTotal = totalRecords,
        //            recordsFiltered = totalRecords,
        //            data = data,
        //            errorCode = 200
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logs.Write("Customer", "GetCustomers", ex.InnerException?.Message ?? ex.Message);

        //        return new
        //        {
        //            errorCode = 999,
        //            errorMessage = ex.Message
        //        };
        //    }
        //}

        //public async Task<dynamic> GetCustomers(IList<QueryFilters> filters)
        //{
        //    try
        //    {
        //        var query =
        //            from c in _context.Customers

        //                // 🔹 join CreatedBy user
        //            join u in _context.Users on c.CreatedBy equals u.Id into userJoin
        //            from user in userJoin.DefaultIfEmpty()

        //            select new
        //            {
        //                c.Id,
        //                c.CustomerName,
        //                c.Email,
        //                c.MailingName,
        //                c.VatId,
        //                CreatedBy = user.UserName,
        //                c.CreatedOn,
        //                c.IsActive
        //            };

        //        // 🔍 Apply Filters
        //        if (filters != null && filters.Any())
        //        {
        //            foreach (var filter in filters)
        //            {
        //                if (string.IsNullOrEmpty(filter.filterValue))
        //                    continue;

        //                switch (filter.fieldName?.ToLower())
        //                {
        //                    case "customername":
        //                        query = query.Where(x => x.CustomerName.Contains(filter.filterValue));
        //                        break;

        //                    case "email":
        //                        query = query.Where(x => x.Email.Contains(filter.filterValue));
        //                        break;
        //                }
        //            }
        //        }

        //        var result = await query.ToListAsync();

        //        return new
        //        {
        //            errorCode = 200,
        //            data = result
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logs.Write(
        //            "Customer",
        //            "GetCustomers",
        //            ex.InnerException?.Message ?? ex.Message
        //        );

        //        return new
        //        {
        //            errorCode = 999,
        //            errorMessage = ex.Message
        //        };
        //    }
        //}

        public async Task<dynamic> GetCustomerById(int id)
        {
            try
            {
                var customer = await _context.Customers
                                             .FirstOrDefaultAsync(c => c.Id == id);

                if (customer == null)
                {
                    return new
                    {
                        errorCode = 404,
                        errorMessage = "Customer not found"
                    };
                }

                return new
                {
                    errorCode = 200,
                    Customer = new
                    {
                        customer.Id,
                        customer.CustomerCode,
                        customer.TypeId,
                        customer.Language,
                        customer.CompanyId,
                        customer.CustomerName,
                        customer.CustomerNameAr,
                        customer.MailingName,
                        customer.MailingNameAr,
                        customer.Email,
                        customer.ContactNo,
                        customer.Address,
                        customer.AddressAr,
                        customer.VatId,
                        customer.VatDeduction,
                        customer.VatRate,
                        customer.RegionId,
                        customer.CountryId,
                        customer.Pobox,
                        customer.LocalLanguageMailing,
                        customer.IsActive,
                        customer.BankName,
                        customer.AccountNumber,
                        customer.Iban,
                        customer.SwiftCode,

                        // 🔹 Optional audit fields
                        customer.CreatedBy,
                        customer.CreatedOn,
                        customer.ModifiedBy,
                        customer.ModifiedOn,
                    }
                };
            }
            catch (Exception ex)
            {
                _logs.Write(
                    "Customer",
                    "GetCustomerById",
                    ex.InnerException?.Message ?? ex.Message
                );

                return new
                {
                    errorCode = 999,
                    errorMessage = ex.Message
                };
            }
        }

        public async Task<dynamic> GetServiceProviderById(int id)
        {
            try
            {
                // 1. Query lagayi aur directly Select ke zariye sirf required data pick kiya (Join automatically lag jayega)
                var customerData = await (
                    from c in _context.Customers

                    join co in _context.Countries
                        on c.CountryId equals co.Id into countryJoin
                    from co in countryJoin.DefaultIfEmpty()

                    join r in _context.Regions
                        on c.RegionId equals r.Id into regionJoin
                    from r in regionJoin.DefaultIfEmpty()

                    join comp in _context.Companies
                        on c.CompanyId equals comp.Id into companyJoin
                    from comp in companyJoin.DefaultIfEmpty()

                    where c.Id == id && c.TypeId == 2

                    select new
                    {
                        c.Id,
                        c.CustomerCode,
                        c.TypeId,
                        c.CustomerName,
                        c.CustomerNameAr,

                        // Company
                        CompanyName = comp != null ? comp.Name : "",

                        // Address fields
                        c.Address,
                        c.AddressAr,
                        c.Pobox,

                        // Country & Region
                        CountryName = co != null ? co.Name : "",
                        RegionName = r != null ? r.Name : "",

                        c.VatId,
                        c.VatDeduction,
                        c.VatRate,
                        c.BankName,
                        c.AccountNumber,
                        c.Iban,
                        c.SwiftCode,
                    }
                ).FirstOrDefaultAsync();

                // 2. Agar data nahi mila
                if (customerData == null)
                {
                    return new
                    {
                        errorCode = 404,
                        errorMessage = "Service Provider not found"
                    };
                }

                // 3. Success Response
                return new
                {
                    errorCode = 200,
                    Customer = customerData
                };
            }
            catch (Exception ex)
            {
                _logs.Write(
                    "Customer",
                    "GetServiceProviderById", // Log me function name update kar diya hai
                    ex.InnerException?.Message ?? ex.Message
                );

                return new
                {
                    errorCode = 999,
                    errorMessage = ex.Message
                };
            }
        }

        public async Task<dynamic> DeleteCustomerById(int id)
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

                var existingRecord = await _context.Customers
                                                   .FirstOrDefaultAsync(c => c.Id == id);

                if (existingRecord == null)
                {
                    return new
                    {
                        errorCode = 404,
                        errorMessage = "Customer not found"
                    };
                }

                _context.Customers.Remove(existingRecord);
                await _context.SaveChangesAsync();

                return new
                {
                    errorCode = 200,
                    message = "Customer deleted successfully"
                };
            }
            catch (Exception ex)
            {
                _logs.Write(
                    "Customer",
                    "DeleteCustomerById",
                    ex.InnerException?.Message ?? ex.Message
                );

                return new
                {
                    errorCode = 999,
                    errorMessage = ex.Message
                };
            }
        }

        public async Task<dynamic> SetCustomerStatus(int id, bool isActive)
        {
            var customer = await _context.Customers.FindAsync(id);

            if (customer == null)
            {
                return new { errorCode = 404, errorMessage = "Customer not found" };
            }

            customer.IsActive = isActive;
            customer.ModifiedBy = _session.LoginId;
            customer.ModifiedOn = DateTime.Now;

            await _context.SaveChangesAsync();

            return new
            {
                errorCode = 200,
                message = isActive ? "Customer activated" : "Customer deactivated"
            };
        }

        public async Task<dynamic> ToggleCustomerStatus(int id)
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

                var customer = await _context.Customers
                                             .FirstOrDefaultAsync(c => c.Id == id);

                if (customer == null)
                {
                    return new
                    {
                        errorCode = 404,
                        errorMessage = "Customer not found"
                    };
                }

                // 🔄 Toggle status
                customer.IsActive = !(customer.IsActive ?? false);

                customer.ModifiedBy = _session.LoginId;
                customer.ModifiedOn = DateTime.Now;

                await _context.SaveChangesAsync();

                return new
                {
                    errorCode = 200,
                    message = customer.IsActive == true
                                ? "Customer activated successfully"
                                : "Customer deactivated successfully",
                    isActive = customer.IsActive
                };
            }
            catch (Exception ex)
            {
                _logs.Write(
                    "Customer",
                    "ToggleCustomerStatus",
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
}
