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
    public class Account : IAccount
    {
        private readonly AppDbContext _context;
        private readonly ISessionHelper _session;
        private readonly ILogs _logs;
        private readonly IDatabaseObject _db;

        public Account(AppDbContext context, ISessionHelper session, ILogs logs, IDatabaseObject db)
        {
            _context = context;
            _session = session;
            _logs = logs;
            _db = db;
        }

        public void Dispose()
        {
            //   throw new NotImplementedException();
        }

        public async Task<dynamic> Login(string email, string password)
        {

            //string encryptedPassword = EncryptionHelper.Encrypt(password);

            //return new
            //{
            //    errorCode = 300,
            //    data = encryptedPassword
            //};

            try
            {
                string sql1 = $@"SELECT au.Id LoginId, au.UserId, au.Username, au.LoginType
                                FROM Users au 
                                WHERE au.UserId = @UserId AND au.[Password] = @Password AND ISNULL(au.IsActive,0) = 1 AND LoginType IN (1,2)";


                string sql = $@"SELECT 
                                u.Id AS LoginId,
	                            u.UserId,
                                u.UserName,
                                u.LoginType LoginType,
                                u.UserType UserType,
                                u.ReferenceId,
                                u.Photo,
                                STRING_AGG(cc.CostCenterName, ', ') AS CostCenters,
                                STRING_AGG(CAST(cc.Id AS VARCHAR), ',') AS CostCenterIds

                            FROM Users u
                            LEFT JOIN UserCostCenters ucc 
                                ON ucc.UserId = u.Id
                            LEFT JOIN CostCenters cc    
                                ON cc.Id = ucc.CostCenterId

                            WHERE u.UserId = @UserId
                              AND u.[Password] = @Password
                              AND ISNULL(u.IsActive, 0) = 1

                            GROUP BY 
                                u.Id,
                                u.UserId,
                                u.UserName,
	                            u.UserType,
	                            u.LoginType,
                                u.ReferenceId,
                                u.Photo;";

                SqlParameter[] parameter =
                {
                    new SqlParameter { ParameterName = "@UserId", Value = email },
                    new SqlParameter { ParameterName = "@Password", Value = EncryptionHelper.Encrypt(password) } // Enc.getMD5Password(email, password) 
                };

                var result = await _db.FetchTable(sql, parameter, CommandType.Text);
                ErrorResponse errorResponse = result.error;

                if (!errorResponse.Error)
                {
                    if (DataHelper.HasRows(result.table))
                    {
                        DataRow row = result.table.Rows[0];


                        SqlParameter[] updateParams =
                        {
                            new SqlParameter("@Id", DataHelper.intParse(row["LoginId"])), // Logged-in user
                            new SqlParameter("@LastLogin", DateTime.Now)
                        };

                        string updateQuery = @"UPDATE Users SET LastLogin = @LastLogin WHERE Id = @Id";
                        await _db.Execute(updateQuery, updateParams, CommandType.Text);

                        _session.LoginId = DataHelper.intParse(row["LoginId"]);
                        _session.LoginType = DataHelper.intParse(row["LoginType"]);
                        _session.UserType = DataHelper.intParse(row["UserType"]);
                        _session.Dbuserid = DataHelper.stringParse(row["UserId"]);
                        _session.UserName = DataHelper.stringParse(row["Username"]);
                        _session.CostCenters = DataHelper.stringParse(row["CostCenters"]);
                        _session.CostCenterIds = DataHelper.stringParse(row["CostCenterIds"]);
                        _session.ReferenceId = DataHelper.intParse(row["ReferenceId"]);
                        //_session.Login(0, DataHelper.intParse(row["LoginId"]), row["Username"].ToString(), DataHelper.intParse(row["ReferenceId"]));
                        //List<MenuItemViewModel> list = GetMenu();
                        //_session.getmenus(list);

                        // Set Photo into session (safe check for DBNull)
                        var photoValue = row["Photo"] == DBNull.Value ? "default.jpg" : DataHelper.stringParse(row["Photo"]);
                        _session.Photo = photoValue!;

                        // Call Login including photo
                        _session.Login(0, DataHelper.intParse(row["LoginId"]), DataHelper.stringParse(row["Username"]), DataHelper.intParse(row["ReferenceId"]), photoValue!);

                        return new
                        {
                            errorCode = 200,
                            data = DataHelper.intParse(row["UserType"])
                        };
                    }
                    else
                    {
                        return new
                        {
                            errorCode = 401,
                            errorMessage = "Invalid Userid or password"
                        };
                    }
                }
                else
                {
                    return new
                    {
                        errorCode = 999,
                        errorMessage = errorResponse.ErrorList[0].Message
                    };
                }
            }
            catch (Exception ae)
            {
                //_logs.Write("Account", "Login", ae.Message);
                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        #region User
        public async Task<dynamic> SaveUser(User model)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    User user;
                    int Id = 0;

                    // ================= INSERT =================
                    if (model.Id == 0)
                    {
                        bool userExists = _context.Users.Any(u => u.UserId == model.UserId);
                        if (userExists)
                        {
                            return new { errorCode = 201, errorMessage = "Duplicate User" };
                        }

                        var maxId = await _context.Users.MaxAsync(c => (int?)c.Id) ?? 0;
                        Id = maxId + 1;

                        user = new User
                        {
                            Id = Id,
                            CompanyId = 1,
                            UserId = model.UserId,
                            //Password = Enc.getMD5Password(model.UserId, model.Password),
                            Password = EncryptionHelper.Encrypt(model.Password ?? ""),
                            UserName = model.UserName,
                            LoginType = (model.UserType == 2 ? 2 : 3), //2=Admin, 3=User(Authorize/UnAuthorize)
                            ReferenceId = _session.LoginType == 1 ? Id : _session.LoginId,
                            UserType = model.UserType,
                            Email = model.Email,
                            PhoneNo = model.PhoneNo,
                            Cnic = model.Cnic,
                            Photo = model.Photo,
                            IsActive = model.IsActive,
                            CreatedBy = _session.LoginId,
                            CreatedOn = DateTime.Now,
                            CompanyName = model.CompanyName,
                            CompanyNameAr = model.CompanyNameAr,
                            EstablishmentName = model.EstablishmentName,
                            EstablishmentNameAr = model.EstablishmentNameAr,    
                            Country = model.Country,
                            CountryAr = model.CountryAr,
                            City = model.City,
                            CityAr = model.CityAr,
                            Vatnumber = model.Vatnumber,
                        };

                        _context.Users.Add(user);
                        await _context.SaveChangesAsync();
                    }
                    // ================= UPDATE =================
                    else
                    {
                        Id = model.Id;

                        user = await _context.Users.FindAsync(model.Id);
                        if (user == null)
                        {
                            return new { errorCode = 404, errorMessage = "User not found" };
                        }

                        var oldUserId = user.UserId;

                        user.UserId = model.UserId;
                        user.UserName = model.UserName;
                        //user.ReferenceId = (model.ReferenceId != null ? model.ReferenceId : _session.LoginId);
                        user.ReferenceId = _session.LoginType == 1 ? model.ReferenceId : _session.LoginId;
                        user.LoginType = (model.UserType == 2 ? 2 : 3); //2=Admin, 3=User(Authorize/UnAuthorize)
                        user.UserType = model.UserType;
                        user.Email = model.Email;
                        user.PhoneNo = model.PhoneNo;
                        user.Cnic = model.Cnic;
                        user.IsActive = model.IsActive;
                        user.ModifiedBy = _session.LoginId;
                        user.ModifiedOn = DateTime.Now;
                        user.CompanyName = model.CompanyName;
                        user.CompanyNameAr = model.CompanyNameAr;
                        user.EstablishmentName = model.EstablishmentName;
                        user.EstablishmentNameAr = model.EstablishmentNameAr;   
                        user.Country = model.Country;
                        user.CountryAr = model.CountryAr;       
                        user.City = model.City;
                        user.CityAr = model.CityAr;
                        user.Vatnumber = model.Vatnumber;

                        //if (!string.IsNullOrEmpty(model.Password))
                        //    user.Password = Enc.getMD5Password(model.UserId, model.Password);

                        if (!string.IsNullOrEmpty(model.Photo))
                            user.Photo = model.Photo;


                        // CASE 1: UserId change hui
                        if (oldUserId != model.UserId)
                        {
                            // agar password diya hai to new password set karo
                            if (!string.IsNullOrEmpty(model.Password))
                            {
                                user.Password = EncryptionHelper.Encrypt(model.Password); //Enc.getMD5Password(model.UserId, model.Password);
                            }
                            else
                            {
                                // return warning to frontend
                                return new
                                {
                                    errorCode = 409,
                                    errorMessage = "You have changed User ID. Please enter your old password to continue or set a new password."
                                };
                            }
                        }
                        else
                        {
                            // normal update
                            if (!string.IsNullOrEmpty(model.Password))
                            {
                                user.Password = EncryptionHelper.Encrypt(model.Password); //Enc.getMD5Password(model.UserId, model.Password);
                            }
                        }

                        await _context.SaveChangesAsync();

                    }

                    // ================= COST CENTER SAVE =================

                    // Delete old cost centers
                    var oldCostCenters = _context.UserCostCenters.Where(x => x.UserId == Id);
                    _context.UserCostCenters.RemoveRange(oldCostCenters);
                    await _context.SaveChangesAsync();

                    // Insert new cost centers
                    if (model.CostCenterIds != null && model.CostCenterIds.Count > 0)
                    {
                        foreach (var ccId in model.CostCenterIds)
                        {
                            _context.UserCostCenters.Add(new UserCostCenter
                            {
                                UserId = Id,
                                CostCenterId = ccId
                            });
                        }
                        await _context.SaveChangesAsync();
                    }

                    transaction.Commit();

                    return new { errorCode = 200, errorMessage = "User saved successfully" };
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    _logs.Write("Account", "SaveUser", ex.InnerException?.Message ?? ex.Message);

                    return new { errorCode = 999, errorMessage = ex.Message };
                }
            }
        }

        public async Task<dynamic> SaveUserBk(User model)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    if (model.Id == 0)
                    {
                        var userExists = _context.Users.Any(u => u.UserId == model.UserId);

                        if (userExists == true)
                        {
                            return new
                            {
                                errorCode = 201,
                                data = "Duplicate User"
                            };
                        }

                        var maxId = await _context.Users.MaxAsync(c => (int?)c.Id) ?? 0;

                        var user = new User
                        {
                            Id = maxId + 1,
                            CompanyId = 1, //model.CompanyId,
                            UserId = model.UserId,
                            Password = Enc.getMD5Password(model.UserId, model.Password),
                            UserName = model.UserName,
                            LoginType = (model.UserType != 1 ? 2 : 1),
                            ReferenceId = _session.LoginId,
                            UserType = model.UserType,
                            CostCenter = model.CostCenter,
                            Email = model.Email,
                            PhoneNo = model.PhoneNo,
                            Cnic = model.Cnic,
                            Photo = model.Photo,
                            IsActive = model.IsActive,
                            CreatedBy = _session.LoginId,
                            CreatedOn = DateTime.Now,
                        };

                        
                        _context.Users.Add(user);
                        await _context.SaveChangesAsync();

                        //// Save Cost Centers
                        //if (model.CostCenterIds != null)
                        //{
                        //    foreach (var ccId in model.CostCenterIds)
                        //    {
                        //        _context.UserCostCenters.Add(new UserCostCenter
                        //        {
                        //            UserId = user.Id,
                        //            CostCenterId = ccId
                        //        });
                        //    }
                        //}
                    }
                    else
                    {
                        var existingRecord = await _context.Users.FindAsync(model.Id);

                        if (existingRecord == null)
                        {
                            return new
                            {
                                errorCode = 404,
                                errorMessage = "User not found"
                            };
                        }

                        // Update existing record
                        existingRecord.UserId = model.UserId;
                        existingRecord.UserName = model.UserName;
                        existingRecord.ReferenceId = model.ReferenceId;

                        // LoginType logic
                        existingRecord.LoginType = (model.UserType != 1 ? 2 : 1);

                        // Update password only if new password is provided
                        if (!string.IsNullOrEmpty(model.Password))
                        {
                            existingRecord.Password = Enc.getMD5Password(model.UserId, model.Password);
                        }

                        // Update UserType
                        existingRecord.UserType = model.UserType;

                        // Cost Center / Contact Info
                        existingRecord.CostCenter = model.CostCenter;
                        existingRecord.Email = model.Email;
                        existingRecord.PhoneNo = model.PhoneNo;
                        existingRecord.Cnic = model.Cnic;

                        // Update Photo only if new uploaded
                        if (!string.IsNullOrEmpty(model.Photo))
                        {
                            existingRecord.Photo = model.Photo;
                        }

                        // Status update
                        existingRecord.IsActive = model.IsActive;

                        // Audit fields
                        existingRecord.ModifiedBy = _session.LoginId;
                        existingRecord.ModifiedOn = DateTime.Now;


                        await _context.SaveChangesAsync();
                    }

                    //await _context.SaveChangesAsync();

                    transaction.Commit();

                    return new
                    {
                        errorCode = 200,
                        data = "User saved successfully"
                    };
                }
                catch (Exception ae)
                {
                    transaction.Rollback();

                    if (ae.InnerException != null)
                    {
                        _logs.Write("Account", "SaveUser", ae.InnerException.Message);
                    }
                    else
                    {
                        _logs.Write("Account", "SaveUser", ae.Message);
                    }

                    return new
                    {
                        errorCode = 999,
                        errorMessage = ae.Message
                    };
                }
            }
        }

        public async Task<dynamic> GetUser(IList<QueryFilters> filters)
        {
            try
            {
                var query = from u in _context.Users

                            join us in _context.Users on u.CreatedBy equals us.Id into userJoin
                            from user in userJoin.DefaultIfEmpty()

                            join ut in _context.UserTypes on u.UserType equals ut.Id into typeJoin
                            from userType in typeJoin.DefaultIfEmpty()

                            select new
                            {
                                u.Id,
                                u.UserId,
                                u.Password,
                                u.UserName,
                                UserTypeId = userType.Id,
                                UserTypeName = userType.Name,
                                CreatedBy = user.UserName,
                                u.CreatedOn,
                                u.IsActive,
                                u.ReferenceId,

                                CostCenters = (
                                    from uc in _context.UserCostCenters
                                    join cc in _context.CostCenters
                                        on uc.CostCenterId equals cc.Id
                                    where uc.UserId == u.Id
                                    select cc.CostCenterName
                                ).ToList()
                            };

                // Apply filter based on session
                if (_session.LoginType != 1)
                {
                    query = query.Where(x => x.ReferenceId == _session.LoginId);
                }
                else
                {
                    query = query.Where(x => x.UserTypeId == 2);
                }

                    var data = await query.ToListAsync();

                var result = data.Select(x => new
                {
                    x.Id,
                    x.UserId,
                    x.Password,
                    x.UserName,
                    x.UserTypeId,
                    x.UserTypeName,
                    x.CreatedBy,
                    x.CreatedOn,
                    x.IsActive,
                    x.ReferenceId,
                    CostCenters = string.Join(", ", x.CostCenters)
                }).ToList();

                return new
                {
                    errorCode = 200,
                    data = result
                };
            }
            catch (Exception ae)
            {
                if (ae.InnerException != null)
                {
                    _logs.Write("Account", "GetUser", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Account", "GetUser", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetUserBk(IList<QueryFilters> filters)
        {
            try
            {
                var query = from u in _context.Users

                            join us in _context.Users on u.CreatedBy equals us.Id into userJoin
                            from user in userJoin.DefaultIfEmpty()

                            join ut in _context.UserTypes on u.UserType equals ut.Id into typeJoin
                            from userType in typeJoin.DefaultIfEmpty()

                            select new
                            {
                                u.Id,
                                u.UserId,
                                u.UserName,
                                UserTypeId = userType.Id,
                                UserTypeName = userType.Name,
                                CreatedBy = user.UserName,
                                u.CreatedOn,
                                u.IsActive,
                                u.ReferenceId
                            };


                // ✔️ Apply filter based on session
                if (_session.LoginType != 1)
                {
                    query = query.Where(x => x.ReferenceId == _session.LoginId);
                }

                var result = await query.ToListAsync();

                return new
                {
                    errorCode = 200,
                    data = result
                };
            }
            catch (Exception ae)
            {
                if (ae.InnerException != null)
                {
                    _logs.Write("Account", "GetUser", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Account", "GetUser", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetUserById(int Id)
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(r => r.Id == Id);

                if (user == null)
                {
                    return new
                    {
                        errorCode = 404,
                        errorMessage = "User not found"
                    };
                }

                // Get Cost Centers
                var costCenterIds = await _context.UserCostCenters
                    .Where(x => x.UserId == Id)
                    .Select(x => x.CostCenterId)
                    .ToListAsync();

                if (user != null)
                {
                    return new
                    {
                        errorCode = 200,
                        User = new
                        {
                            user.Id,
                            user.CompanyId,
                            user.UserId,
                            user.Password,
                            user.UserName,
                            user.LoginType,
                            user.ReferenceId,
                            user.UserType,
                            user.CostCenter,
                            user.Email,
                            user.PhoneNo,
                            user.Cnic,
                            user.Photo,
                            user.IsActive,
                            user.CompanyName,
                            user.CompanyNameAr,
                            user.EstablishmentName,
                            user.EstablishmentNameAr,
                            user.Country,
                            user.CountryAr,
                            user.City,
                            user.CityAr,
                            user.Vatnumber,
                            CostCenterIds = costCenterIds
                        },
                    };
                }
                else
                {
                    return new
                    {
                        errorCode = 404,
                        errorMessage = "User not found"
                    };
                }
            }
            catch (Exception ae)
            {
                if (ae.InnerException != null)
                {
                    _logs.Write("Account", "GetUserById", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Account", "GetUserById", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> DeleteUserById(int Id)
        {
            try
            {
                if (Id == 0)
                {
                    return new
                    {
                        errorMessage = "Error"
                    };
                }
                else
                {
                    var existingRecord = await _context.Users.Where(a => a.Id == Id).FirstOrDefaultAsync();
                    if (existingRecord == null)
                    {
                        return new
                        {
                            errorMessage = "User not found"
                        };
                    }

                    var userCostCenters = _context.UserCostCenters.Where(uc => uc.UserId == Id);
                    _context.UserCostCenters.RemoveRange(userCostCenters);

                    _context.Users.Remove(existingRecord);
                    await _context.SaveChangesAsync();

                    return new
                    {
                        errorCode = 200,
                        message = "User deleted successfully"
                    };
                }
            }
            catch (Exception ae)
            {
                string message = ae.Message;
                if (ae.InnerException != null)
                {
                    message = ae.InnerException.Message;
                }

                _logs.Write("Account", "DeleteUserById", message);

                return new
                {
                    errorCode = 999,
                    errorMessage = message
                };
            }
        }

        public async Task<dynamic> ToggleUserStatus(int id)
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

                    var record = await _context.Users
                                               .FirstOrDefaultAsync(c => c.Id == id);

                    if (record == null)
                    {
                        return new
                        {
                            errorCode = 404,
                            errorMessage = "User not found"
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
                        message = "User status updated successfully",
                        isActive = record.IsActive
                    };
                }
                catch (Exception ex)
                {
                    transaction.Rollback();

                    _logs.Write(
                        "User",
                        "ToggleUserStatus",
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

        public async Task<dynamic> GetProfile()
        {
            try
            {
                if (_session.LoginId == 0)
                {
                    return new
                    {
                        errorCode = 401,
                        errorMessage = "Session expired"
                    };
                }

                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == _session.LoginId);

                if (user == null)
                {
                    return new
                    {
                        errorCode = 404,
                        errorMessage = "User not found"
                    };
                }

                return new
                {
                    errorCode = 200,
                    user = new
                    {
                        user.Id,
                        user.UserId,
                        user.UserName,
                        user.Email,
                        user.PhoneNo,
                        user.Cnic,
                        user.Photo
                    }
                };
            }
            catch (Exception ex)
            {
                _logs.Write("Account", "GetProfile", ex.InnerException?.Message ?? ex.Message);

                return new
                {
                    errorCode = 999,
                    errorMessage = ex.Message
                };
            }
        }

        public async Task<dynamic> UpdateProfile(User model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (_session.LoginId == 0)
                {
                    return new
                    {
                        errorCode = 401,
                        errorMessage = "Session expired"
                    };
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(x => x.Id == _session.LoginId);

                if (user == null)
                {
                    return new
                    {
                        errorCode = 404,
                        errorMessage = "User not found"
                    };
                }

                // Only update fields that belong to the user's profile.
                user.UserName = model.UserName;
                user.Email = model.Email;
                user.PhoneNo = model.PhoneNo;
                user.Cnic = model.Cnic;
                user.ModifiedBy = _session.LoginId;
                user.ModifiedOn = DateTime.Now;

                // Password is optional during a profile update.
                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    user.Password = EncryptionHelper.Encrypt(model.Password);
                }

                // Do not overwrite the existing photo when no new photo was uploaded.
                if (!string.IsNullOrWhiteSpace(model.Photo))
                {
                    user.Photo = model.Photo;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Keep the layout header synchronized with the updated profile.
                _session.UserName = user.UserName ?? "";
                _session.Photo = string.IsNullOrWhiteSpace(user.Photo)
                    ? "default.jpg"
                    : user.Photo;

                return new
                {
                    errorCode = 200,
                    errorMessage = "Profile updated successfully"
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _logs.Write("Account", "UpdateProfile", ex.InnerException?.Message ?? ex.Message);

                return new
                {
                    errorCode = 999,
                    errorMessage = ex.Message
                };
            }
        }

        public async Task<(string CompanyName, string CompanyNameAr)> GetCompanyNamesAsync()
        {
            if (_session.LoginId == 0)
            {
                return (string.Empty, string.Empty);
            }

            var companyNames = await _context.Users
                .AsNoTracking()
                .Where(x => x.Id == _session.LoginId)
                .Select(x => new
                {
                    x.CompanyName,
                    x.CompanyNameAr
                })
                .FirstOrDefaultAsync();

            if (companyNames == null)
            {
                return (string.Empty, string.Empty);
            }

            return (
                companyNames.CompanyName ?? string.Empty,
                companyNames.CompanyNameAr ?? string.Empty
            );
        }
        #endregion User
    }
}
