using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Services
{
    public class Dropdown : IDropdown
    {
        private readonly AppDbContext _context;
        private readonly ILogs _logs;
        private readonly ISessionHelper _session;

        public Dropdown(AppDbContext context, ILogs logs, ISessionHelper session)
        {
            _context = context;
            _logs = logs;
            _session = session; 
        }

        public async Task<dynamic> GetUser()
        {
            try
            {
                //var result = await _context.Users.Where(r => r.IsActive == true && r.ReferenceId == _session.ReferenceId).ToListAsync();

                var result = await _context.Users.Where(r => r.IsActive == true && r.LoginType != 1 && (_session.LoginType == 1 || r.ReferenceId == _session.ReferenceId)).ToListAsync();

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
                    _logs.Write("Dropdown", "GetUser", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetUser", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetAdmin()
        {
            try
            {
                var result = await _context.Users.Where(r => r.IsActive == true && r.UserType == 2).ToListAsync();

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
                    _logs.Write("Dropdown", "GetUser", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetUser", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetUserType()
        {
            try
            {
                var query = _context.UserTypes.Where(r => r.IsActive == true && r.Id != 1);

                //if (_session.LoginType == 2)
                //{
                //    query = query.Where(r => r.Id != 2);
                //}

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
                    _logs.Write("Dropdown", "GetUserType", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetUserType", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetCostCenter()
        {
            try
            {
                var result = await _context.CostCenters.Where(r => r.IsActive == true && r.ReferenceId == _session.ReferenceId).ToListAsync();

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
                    _logs.Write("Dropdown", "GetCostCenter", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetCostCenter", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetCostCenterAssigned()
        {
            try
            {
                IQueryable<CostCenter> query = _context.CostCenters.Where(c => c.IsActive == true);

                if (_session.UserType == 2)
                {
                    // Admin / Company User
                    query = query.Where(c => c.ReferenceId == _session.ReferenceId);
                }
                else
                {
                    // Assigned Cost Centers Only
                    query = query.Where(c =>
                        _context.UserCostCenters.Any(uc =>
                            uc.UserId == _session.LoginId &&
                            uc.CostCenterId == c.Id));
                }

                var result = await query
                    .OrderBy(c => c.CostCenterName)
                    .ToListAsync();

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
                    _logs.Write("Dropdown", "GetCostCenterAssigned", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetCostCenterAssigned", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetCountry()
        {
            try
            {
                var result = await _context.Countries.Where(r => r.IsActive == true).ToListAsync();

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
                    _logs.Write("Dropdown", "GetCountry", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetCountry", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetRegion()
        {
            try
            {
                var result = await _context.Regions.Where(r => r.IsActive == true).ToListAsync();

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
                    _logs.Write("Dropdown", "GetRegion", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetRegion", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetCompany()
        {
            try
            {
                var result = await _context.Companies.Where(r => r.IsActive == true && (_session.LoginType == 1 || r.CreatedBy == _session.ReferenceId)).ToListAsync();

                //var result = await _context.Companies.Where(r => r.IsActive == true && r.CreatedBy == _session.ReferenceId).ToListAsync();

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
                    _logs.Write("Dropdown", "GetCompany", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetCompany", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetCustomer()
        {
            try
            {
                var result = await _context.Customers.Where(r => r.IsActive == true && r.TypeId == 1 && r.ReferenceId == _session.ReferenceId).ToListAsync();

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
                    _logs.Write("Dropdown", "GetCustomer", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetCustomer", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetCustomerSearch(string search = "", int page = 1)
        {
            int pageSize = 10;

            var query = _context.Customers.AsQueryable();

            query = query.Where(r => r.IsActive == true && r.TypeId == 1 && r.ReferenceId == _session.ReferenceId);

            //if (!string.IsNullOrWhiteSpace(search))
            //{
            //    query = query.Where(x =>
            //        x.CustomerName != null &&
            //        (
            //            x.CustomerName.Contains(search) ||
            //            (x.CustomerCode != null && x.CustomerCode.Contains(search)) ||
            //            x.Id.ToString() == search
            //        )
            //    );
            //}

            if (!string.IsNullOrWhiteSpace(search))
            {
                int.TryParse(search, out int customerId);

                query = query.Where(x =>
                    (x.CustomerName != null && x.CustomerName.Contains(search)) ||
                    (x.CustomerCode != null && x.CustomerCode.Contains(search)) ||
                    (customerId > 0 && x.Id == customerId)
                );
            }

            var total = await query.CountAsync();

            var data = await query
                .OrderByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    id = x.Id,
                    text = x.CustomerName + " (" + x.CustomerCode + ")"
                })
                .ToListAsync();

            return new
            {
                errorCode = 200,
                data,
                hasMore = (page * pageSize) < total
            };
        }

        public async Task<dynamic> GetServiceProvider()
        {
            try
            {
                var result = await _context.Customers.Where(r => r.IsActive == true && r.TypeId == 2 && r.ReferenceId == _session.ReferenceId).ToListAsync();

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
                    _logs.Write("Dropdown", "GetServiceProvider", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetServiceProvider", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetServiceProviderSearch(string search = "", int page = 1)
        {
            int pageSize = 10;

            var query = _context.Customers.AsQueryable();

            query = query.Where(r => r.IsActive == true && r.TypeId == 2 && r.ReferenceId == _session.ReferenceId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    x.CustomerName != null &&
                    (
                        x.CustomerName.Contains(search) ||
                        (x.CustomerCode != null && x.CustomerCode.Contains(search))
                    )
                );
            }

            var total = await query.CountAsync();

            var data = await query
                .OrderByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    id = x.Id,
                    text = x.CustomerName + " (" + x.CustomerCode + ")"
                })
                .ToListAsync();

            return new
            {
                errorCode = 200,
                data,
                hasMore = (page * pageSize) < total
            };
        }

        public async Task<dynamic> GetShipmentMode()
        {
            try
            {
                var result = await _context.ShipmentModes.Where(r => r.IsActive == true).ToListAsync();

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
                    _logs.Write("Dropdown", "GetShipmentMode", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetShipmentMode", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetJobType()
        {
            try
            {
                var result = await _context.JobTypes
                    .Where(r => r.IsActive == true)
                    .ToListAsync();

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
                    _logs.Write("Dropdown", "GetJobType", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetJobType", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetContainerType()
        {
            try
            {
                var result = await _context.ContainerTypes
                    .Where(r => r.IsActive == true)
                    .ToListAsync();

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
                    _logs.Write("Dropdown", "GetContainerType", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetContainerType", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetBLType()
        {
            try
            {
                var result = await _context.Bltypes
                    .Where(r => r.IsActive == true)
                    .ToListAsync();

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
                    _logs.Write("Dropdown", "GetBLType", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetBLType", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetPol()
        {
            try
            {
                var result = await _context.Pols
                    .Where(r => r.IsActive == true && r.CreatedBy == _session.ReferenceId)
                    .ToListAsync();

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
                    _logs.Write("Dropdown", "GetPol", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetPol", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetPod()
        {
            try
            {
                var result = await _context.Pods
                    .Where(r => r.IsActive == true && r.CreatedBy == _session.ReferenceId)
                    .ToListAsync();

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
                    _logs.Write("Dropdown", "GetPod", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetPod", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetDeliveryCity()
        {
            try
            {
                var result = await _context.DeliveryCities
                    .Where(r => r.IsActive == true && r.CreatedBy == _session.ReferenceId)
                    .ToListAsync();

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
                    _logs.Write("Dropdown", "GetDeliveryCity", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetDeliveryCity", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetShipmentStatus()
        {
            try
            {
                var result = await _context.ShipmentStatuses
                    .Where(r => r.IsActive == true)
                    .ToListAsync();

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
                    _logs.Write("Dropdown", "GetShipmentStatus", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetShipmentStatus", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetBLStatus()
        {
            try
            {
                var result = await _context.Blstatuses
                    .Where(r => r.IsActive == true)
                    .ToListAsync();

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
                    _logs.Write("Dropdown", "GetBLStatus", ae.InnerException.Message);
                }
                else
                {
                    _logs.Write("Dropdown", "GetBLStatus", ae.Message);
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = ae.Message
                };
            }
        }

        public async Task<dynamic> GetPaymentHeadersByType(int paymentTypeId)
        {
            try
            {
                var result = await _context.PaymentHeaders
                    .Where(x => x.PaymentTypeId == paymentTypeId && x.IsActive == true)
                    .OrderBy(x => x.Headers)
                    .Select(x => new
                    {
                        x.Id,
                        x.Headers,
                        x.HeadersAr,
                        x.Vat,
                        CombinedHeader = (x.Headers ?? "") + " - " + (x.HeadersAr ?? "")
                    })
                    .ToListAsync();

                return new
                {
                    errorCode = 200,
                    data = result
                };
            }
            catch (Exception ex)
            {
                _logs.Write("Dropdown", "GetPaymentHeadersByType",
                    ex.InnerException?.Message ?? ex.Message);

                return new
                {
                    errorCode = 999,
                    errorMessage = ex.Message
                };
            }
        }

        public async Task<dynamic> GetJobNos(string search = "", int page = 1)
        {
            int pageSize = 10;

            var query = _context.JobImportMasters.AsQueryable();
            query = query.Where(x => x.ReferenceId == _session.ReferenceId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x => x.JobNumber != null && x.JobNumber.Contains(search));
            }

            var total = await query.CountAsync();

            var data = await query
                .OrderBy(x => x.JobNumber)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    id = x.Id,
                    text = x.JobNumber
                })
                .ToListAsync();

            return new
            {
                errorCode = 200,
                data,
                hasMore = (page * pageSize) < total
            };
        }
    }
}
