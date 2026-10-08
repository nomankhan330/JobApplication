using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace JobApplication.Controllers
{
    public class DashboardController : BaseController
    {
        //private readonly IDashboard _service;
        private readonly ISessionHelper _session;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IJobImportMaster _jobImportMaster;
        private readonly IMemoryCache _cache;
        private readonly AppDbContext _dbContext;

        public DashboardController(ISessionHelper session, IWebHostEnvironment webHostEnvironment, IJobImportMaster jobImportMaster, IMemoryCache cache, AppDbContext dbContext) : base(session)
        {
            //_service = service;
            _session = session;
            _webHostEnvironment = webHostEnvironment;
            _jobImportMaster = jobImportMaster;
            _cache = cache;
            _dbContext = dbContext;
        }

        /// <summary>
        /// Resolves the month filter value.
        /// Supported values: "all", "current", "previous" or "yyyy-MM".
        /// Returns false when no (valid) month value was provided.
        /// </summary>
        private static bool TryResolveMonth(string? month, DateTime now, out bool isAll, out DateTime from, out DateTime to)
        {
            isAll = false;
            from = default;
            to = default;

            if (string.IsNullOrWhiteSpace(month))
                return false;

            if (month.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                isAll = true;
                return true;
            }

            DateTime monthStart;

            if (month.Equals("current", StringComparison.OrdinalIgnoreCase))
            {
                monthStart = new DateTime(now.Year, now.Month, 1);
            }
            else if (month.Equals("previous", StringComparison.OrdinalIgnoreCase))
            {
                monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
            }
            else if (DateTime.TryParseExact(month.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var yearMonth))
            {
                monthStart = new DateTime(yearMonth.Year, yearMonth.Month, 1);
            }
            else
            {
                return false;
            }

            from = monthStart;
            to = monthStart.AddMonths(1).AddSeconds(-1); // last day 23:59:59
            return true;
        }

        public IActionResult Index2()
        {
            return View();
        }

        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Complete()
        {
            return View();
        }

        public async Task<IActionResult> GetStatisticsJobsCount()
        {
            try
            {
                async Task<int> CountAsync(string? shipmentType = null, DateTime? from = null, DateTime? to = null)
                {
                    var filters = new List<BusinessLogic.Models.QueryFilters>
                    {
                        new() { fieldName = "draw", filterValue = "1" },
                        new() { fieldName = "start", filterValue = "0" },
                        new() { fieldName = "length", filterValue = "1" }
                    };

                    if (!string.IsNullOrEmpty(shipmentType))
                        filters.Add(new() { fieldName = "ShipmentType", filterValue = shipmentType });

                    if (from.HasValue)
                        filters.Add(new() { fieldName = "FromDate", filterValue = from.Value.ToString("yyyy-MM-dd") });

                    if (to.HasValue)
                        filters.Add(new() { fieldName = "ToDate", filterValue = to.Value.ToString("yyyy-MM-dd") });

                    return await _jobImportMaster.CountJobs(filters);
                }

                var now = DateTime.Now;
                var currentStart = new DateTime(now.Year, now.Month, 1);
                var currentEnd = currentStart.AddMonths(1).AddDays(-1);
                var prevStart = currentStart.AddMonths(-1);
                var prevEnd = currentStart.AddDays(-1);

                // Sequential (DbContext safe)
                int totalJobsCurrent = await CountAsync(null, currentStart, currentEnd);
                int totalJobsPrev = await CountAsync(null, prevStart, prevEnd);
                int importJobsCurrent = await CountAsync("1", currentStart, currentEnd);
                int importJobsPrev = await CountAsync("1", prevStart, prevEnd);
                int exportJobsCurrent = await CountAsync("2", currentStart, currentEnd);
                int exportJobsPrev = await CountAsync("2", prevStart, prevEnd);

                static string FormatTrend(int current, int previous, out string cssClass)
                {
                    if (previous == 0)
                    {
                        cssClass = "trend-badge-success";
                        return current == 0 ? "0%" : "+100%";
                    }

                    double percent = Math.Round((current - previous) * 100.0 / previous, 1);

                    if (percent > 0)
                    {
                        cssClass = "trend-badge-success";
                        return $"+{percent}%";
                    }
                    if (percent < 0)
                    {
                        cssClass = "trend-badge-danger";
                        return $"{percent}%";
                    }

                    cssClass = "trend-badge-success";
                    return "0%";
                }

                string totalTrendClass, importTrendClass, exportTrendClass;

                var totalTrendText = FormatTrend(totalJobsCurrent, totalJobsPrev, out totalTrendClass);
                var importTrendText = FormatTrend(importJobsCurrent, importJobsPrev, out importTrendClass);
                var exportTrendText = FormatTrend(exportJobsCurrent, exportJobsPrev, out exportTrendClass);

                return Json(new
                {
                    totalJobs = totalJobsCurrent,
                    importJobs = importJobsCurrent,
                    exportJobs = exportJobsCurrent,

                    totalJobsTrendText = totalTrendText,
                    importJobsTrendText = importTrendText,
                    exportJobsTrendText = exportTrendText,

                    totalJobsTrendClass = totalTrendClass,
                    importJobsTrendClass = importTrendClass,
                    exportJobsTrendClass = exportTrendClass
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = true, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetStatistics()
        {
            //const string cacheKey = "Dashboard_Statistics_v1";

            //if (_cache.TryGetValue(cacheKey, out object cachedResult))
            //{
            //    return Json(cachedResult);
            //}

            try
            {
                async Task<int> CountAsync(string? shipmentType = null, DateTime? from = null, DateTime? to = null)
                {
                    var filters = new List<BusinessLogic.Models.QueryFilters>
                    {
                        new() { fieldName = "draw", filterValue = "1" },
                        new() { fieldName = "start", filterValue = "0" },
                        new() { fieldName = "length", filterValue = "1" }
                    };

                    if (!string.IsNullOrEmpty(shipmentType))
                        filters.Add(new() { fieldName = "ShipmentType", filterValue = shipmentType });

                    if (from.HasValue)
                        filters.Add(new() { fieldName = "FromDate", filterValue = from.Value.ToString("yyyy-MM-dd") });

                    if (to.HasValue)
                        filters.Add(new() { fieldName = "ToDate", filterValue = to.Value.ToString("yyyy-MM-dd") });

                    return await _jobImportMaster.CountJobs(filters);
                }

                var now = DateTime.Now;
                var currentStart = new DateTime(now.Year, now.Month, 1);
                var currentEnd = currentStart.AddMonths(1).AddDays(-1);
                var prevStart = currentStart.AddMonths(-1);
                var prevEnd = currentStart.AddDays(-1);

                // ========== Sequential (Safe) ==========
                int totalJobsCurrent = await CountAsync(null, currentStart, currentEnd);
                int totalJobsPrev = await CountAsync(null, prevStart, prevEnd);
                int importJobsCurrent = await CountAsync("1", currentStart, currentEnd);
                int importJobsPrev = await CountAsync("1", prevStart, prevEnd);
                int exportJobsCurrent = await CountAsync("2", currentStart, currentEnd);
                int exportJobsPrev = await CountAsync("2", prevStart, prevEnd);

                var financial = await _jobImportMaster.GetFinancialStatisticsAsync(currentStart, currentEnd, prevStart, prevEnd);

                // ========== Trend Helper ==========
                static string FormatTrend(decimal current, decimal previous, out string cssClass)
                {
                    if (previous == 0)
                    {
                        cssClass = "trend-badge-success";
                        return current == 0 ? "0%" : "+100%";
                    }

                    double percent = Math.Round((double)((current - previous) * 100m / previous), 1);

                    if (percent > 0)
                    {
                        cssClass = "trend-badge-success";
                        return $"+{percent}%";
                    }
                    if (percent < 0)
                    {
                        cssClass = "trend-badge-danger";
                        return $"{percent}%";
                    }

                    cssClass = "trend-badge-success";
                    return "0%";
                }

                string totalTrendClass, importTrendClass, exportTrendClass;
                var totalTrendText = FormatTrend(totalJobsCurrent, totalJobsPrev, out totalTrendClass);
                var importTrendText = FormatTrend(importJobsCurrent, importJobsPrev, out importTrendClass);
                var exportTrendText = FormatTrend(exportJobsCurrent, exportJobsPrev, out exportTrendClass);

                string salesTrendClass, spTrendClass, netCashTrendClass;
                var salesTrendText = FormatTrend(financial.SalesRevenue, financial.SalesRevenuePrevious, out salesTrendClass);
                var spTrendText = FormatTrend(financial.PurchaseCurrent, financial.PurchasePrevious, out spTrendClass);
                var netCashTrendText = FormatTrend(financial.NetCashInflow, financial.NetCashInflowPrevious, out netCashTrendClass);

                var result = new
                {
                    totalJobs = totalJobsCurrent,
                    importJobs = importJobsCurrent,
                    exportJobs = exportJobsCurrent,
                    totalJobsTrendText = totalTrendText,
                    importJobsTrendText = importTrendText,
                    exportJobsTrendText = exportTrendText,
                    totalJobsTrendClass = totalTrendClass,
                    importJobsTrendClass = importTrendClass,
                    exportJobsTrendClass = exportTrendClass,

                    salesRevenue = financial.SalesRevenue,
                    salesRevenueTrendText = salesTrendText,
                    salesRevenueTrendClass = salesTrendClass,

                    spOutstanding = financial.SpOutstanding,
                    spOutstandingTrendText = spTrendText,
                    spOutstandingTrendClass = spTrendClass,

                    netCashInflow = financial.NetCashInflow,
                    netCashInflowTrendText = netCashTrendText,
                    netCashInflowTrendClass = netCashTrendClass
                };

                // Cache for 25 seconds
                //_cache.Set(cacheKey, result, new MemoryCacheEntryOptions()
                //    .SetAbsoluteExpiration(TimeSpan.FromSeconds(25)));

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = true, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardData(string period = "ThisMonth", string? month = null)
        {
            //string cacheKey = $"Dashboard_FullData_{period}";

            //// Cache check
            //if (_cache.TryGetValue(cacheKey, out object cached))
            //{
            //    return Json(cached);
            //}

            try
            {
                // ==================== 1. Statistics (aapka existing logic) ====================
                async Task<int> CountAsync(string? shipmentType = null, DateTime? from = null, DateTime? to = null)
                {
                    var filters = new List<BusinessLogic.Models.QueryFilters>
                    {
                        new() { fieldName = "draw", filterValue = "1" },
                        new() { fieldName = "start", filterValue = "0" },
                        new() { fieldName = "length", filterValue = "1" }
                    };

                    if (!string.IsNullOrEmpty(shipmentType))
                        filters.Add(new() { fieldName = "ShipmentType", filterValue = shipmentType });

                    if (from.HasValue)
                        filters.Add(new() { fieldName = "FromDate", filterValue = from.Value.ToString("yyyy-MM-dd") });

                    if (to.HasValue)
                        filters.Add(new() { fieldName = "ToDate", filterValue = to.Value.ToString("yyyy-MM-dd") });

                    return await _jobImportMaster.CountJobs(filters);
                }

                var now = DateTime.Now;

                // Month filter: "all" | "current" | "previous" | "yyyy-MM"
                bool monthResolved = TryResolveMonth(month, now, out bool monthIsAll, out DateTime monthFrom, out DateTime monthTo);

                DateTime currentStart, currentEnd, prevStart, prevEnd;

                if (monthResolved && monthIsAll)
                {
                    // All time: wide range (prev same as current so trends stay neutral)
                    currentStart = new DateTime(2000, 1, 1);
                    currentEnd = now;
                    prevStart = currentStart;
                    prevEnd = currentEnd;
                }
                else if (monthResolved)
                {
                    currentStart = monthFrom;
                    currentEnd = monthTo;
                    prevStart = currentStart.AddMonths(-1);
                    prevEnd = currentStart.AddSeconds(-1);
                }
                else
                {
                    currentStart = new DateTime(now.Year, now.Month, 1);
                    currentEnd = currentStart.AddMonths(1).AddSeconds(-1);
                    prevStart = currentStart.AddMonths(-1);
                    prevEnd = currentStart.AddSeconds(-1);
                }

                bool allTime = monthResolved && monthIsAll;
                DateTime? countFrom = allTime ? null : currentStart;
                DateTime? countTo = allTime ? null : currentEnd;

                // Sequential (DbContext safe)
                int totalJobsCurrent = await CountAsync(null, countFrom, countTo);
                int totalJobsPrev = allTime ? totalJobsCurrent : await CountAsync(null, prevStart, prevEnd);
                int importJobsCurrent = await CountAsync("1", countFrom, countTo);
                int importJobsPrev = allTime ? importJobsCurrent : await CountAsync("1", prevStart, prevEnd);
                int exportJobsCurrent = await CountAsync("2", countFrom, countTo);
                int exportJobsPrev = allTime ? exportJobsCurrent : await CountAsync("2", prevStart, prevEnd);

                var financial = await _jobImportMaster.GetFinancialStatisticsAsync(currentStart, currentEnd, prevStart, prevEnd);

                // Trend Helper
                static string FormatTrend(decimal current, decimal previous, out string cssClass)
                {
                    if (previous == 0)
                    {
                        cssClass = "trend-badge-success";
                        return current == 0 ? "0%" : "+100%";
                    }

                    double percent = Math.Round((double)((current - previous) * 100m / previous), 1);

                    if (percent > 0)
                    {
                        cssClass = "trend-badge-success";
                        return $"+{percent}%";
                    }
                    if (percent < 0)
                    {
                        cssClass = "trend-badge-danger";
                        return $"{percent}%";
                    }

                    cssClass = "trend-badge-success";
                    return "0%";
                }

                string totalTrendClass, importTrendClass, exportTrendClass;
                var totalTrendText = FormatTrend(totalJobsCurrent, totalJobsPrev, out totalTrendClass);
                var importTrendText = FormatTrend(importJobsCurrent, importJobsPrev, out importTrendClass);
                var exportTrendText = FormatTrend(exportJobsCurrent, exportJobsPrev, out exportTrendClass);

                string salesTrendClass, spTrendClass, netCashTrendClass;
                var salesTrendText = FormatTrend(financial.SalesRevenue, financial.SalesRevenuePrevious, out salesTrendClass);
                var spTrendText = FormatTrend(financial.PurchaseCurrent, financial.PurchasePrevious, out spTrendClass);
                var netCashTrendText = FormatTrend(financial.NetCashInflow, financial.NetCashInflowPrevious, out netCashTrendClass);

                var statistics = new
                {
                    totalJobs = totalJobsCurrent,
                    importJobs = importJobsCurrent,
                    exportJobs = exportJobsCurrent,
                    totalJobsTrendText = totalTrendText,
                    importJobsTrendText = importTrendText,
                    exportJobsTrendText = exportTrendText,
                    totalJobsTrendClass = totalTrendClass,
                    importJobsTrendClass = importTrendClass,
                    exportJobsTrendClass = exportTrendClass,

                    salesRevenue = financial.SalesRevenue,
                    salesRevenueTrendText = salesTrendText,
                    salesRevenueTrendClass = salesTrendClass,

                    spOutstanding = financial.SpOutstanding,
                    spOutstandingTrendText = spTrendText,
                    spOutstandingTrendClass = spTrendClass,

                    netCashInflow = financial.NetCashInflow,
                    netCashInflowTrendText = netCashTrendText,
                    netCashInflowTrendClass = netCashTrendClass
                };

                // ==================== 2. Recent Invoices ====================
                var recentInvoices = await _jobImportMaster.GetRecentInvoicesAsync(
                    8,
                    monthResolved && monthIsAll ? null : (monthResolved ? monthFrom : null),
                    monthResolved && monthIsAll ? null : (monthResolved ? monthTo : null));

                // ==================== 3. Job Operations Share ====================
                DateTime? opsFrom = null, opsTo = null;
                if (monthResolved)
                {
                    opsFrom = monthIsAll ? new DateTime(2000, 1, 1) : monthFrom;
                    opsTo = monthIsAll ? now : monthTo;
                }
                var jobOperationsShare = await _jobImportMaster.GetJobOperationsShareAsync(period, opsFrom, opsTo);

                // ==================== Final Response ====================
                var result = new
                {
                    statistics,
                    recentInvoices,
                    jobOperationsShare
                };

                // Cache for 30 seconds
                //_cache.Set(cacheKey, result, new MemoryCacheEntryOptions()
                //    .SetAbsoluteExpiration(TimeSpan.FromSeconds(30)));

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { error = true, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSalesPurchaseTrend(string period = "ThisYear", string? from = null, string? to = null, string? month = null)
        {
            try
            {
                var refId = _session.ReferenceId;
                var now = DateTime.Now;
                DateTime fromDate, toDate;

                if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                {
                    // parse provided range (expected format yyyy-MM-dd)
                    fromDate = DateTime.Parse(from);
                    toDate = DateTime.Parse(to);
                }
                else if (TryResolveMonth(month, now, out bool spMonthIsAll, out DateTime spMonthFrom, out DateTime spMonthTo))
                {
                    if (spMonthIsAll)
                    {
                        // All time: start from the earliest invoice available
                        var minSales = await _dbContext.SalesInvoices
                            .Where(x => x.ReferenceId == refId
                                     && (x.IsCancelled == null || x.IsCancelled == false))
                            .MinAsync(x => (DateTime?)x.InvoiceDate);

                        var minPurchases = await _dbContext.PurchaseInvoices
                            .Where(x => x.ReferenceId == refId
                                     && (x.IsCancelled == null || x.IsCancelled == false))
                            .MinAsync(x => (DateTime?)x.InvoiceDate);

                        DateTime? minDate = minSales;
                        if (minPurchases.HasValue && (!minDate.HasValue || minPurchases < minDate))
                            minDate = minPurchases;

                        fromDate = minDate.HasValue
                            ? new DateTime(minDate.Value.Year, minDate.Value.Month, 1)
                            : new DateTime(now.Year, 1, 1);
                        toDate = now;
                    }
                    else
                    {
                        fromDate = spMonthFrom;
                        toDate = spMonthTo;
                    }
                }
                else
                {
                    if (period == "LastMonth")
                    {
                        fromDate = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                        toDate = fromDate.AddMonths(1).AddDays(-1);
                    }
                    else if (period == "ThisMonth")
                    {
                        fromDate = new DateTime(now.Year, now.Month, 1);
                        toDate = now;
                    }
                    else if (period == "LastYear")
                    {
                        fromDate = new DateTime(now.Year - 1, 1, 1);
                        toDate = new DateTime(now.Year - 1, 12, 31);
                    }
                    else // ThisYear (default)
                    {
                        fromDate = new DateTime(now.Year, 1, 1);
                        toDate = now;
                    }
                }

                // 1. Fetch Sales Raw Data for Entire Range (Single DB Query)
                var salesRaw = await _dbContext.SalesInvoices
                    .Where(x => x.ReferenceId == refId
                             && x.InvoiceDate >= fromDate && x.InvoiceDate <= toDate
                             && (x.IsCancelled == null || x.IsCancelled == false))
                    .Select(x => new { x.InvoiceDate, x.GrandTotal })
                    .ToListAsync();

                // 2. Fetch Purchases Raw Data for Entire Range (Single DB Query)
                var purchasesRaw = await _dbContext.PurchaseInvoices
                    .Where(x => x.ReferenceId == refId
                             && x.InvoiceDate >= fromDate && x.InvoiceDate <= toDate
                             && (x.IsCancelled == null || x.IsCancelled == false))
                    .Select(x => new {
                        x.InvoiceDate,
                        Amount = (x.BalanceAmount == null || x.BalanceAmount == 0) ? x.GrandTotal : x.BalanceAmount.Value
                    })
                    .ToListAsync();

                var labels = new List<string>();
                var salesData = new List<decimal>();
                var purchaseData = new List<decimal>();

                var cursor = new DateTime(fromDate.Year, fromDate.Month, 1);
                var endCursor = new DateTime(toDate.Year, toDate.Month, 1);

                // Grouping data in-memory per month
                while (cursor <= endCursor)
                {
                    var monthStart = cursor;
                    var monthEnd = cursor.AddMonths(1);

                    decimal salesSum = salesRaw
                        .Where(x => x.InvoiceDate >= monthStart && x.InvoiceDate < monthEnd)
                        .Sum(x => x.GrandTotal);

                    decimal purchaseSum = purchasesRaw
                        .Where(x => x.InvoiceDate >= monthStart && x.InvoiceDate < monthEnd)
                        .Sum(x => x.Amount);

                    labels.Add(cursor.ToString("MMM yyyy"));
                    salesData.Add(salesSum);
                    purchaseData.Add(purchaseSum);

                    cursor = cursor.AddMonths(1);
                }

                // Totals calculated directly from fetched range
                decimal salesRangeTotal = salesRaw.Sum(x => x.GrandTotal);
                decimal purchasesRangeTotal = purchasesRaw.Sum(x => x.Amount);
                decimal netRange = salesRangeTotal - purchasesRangeTotal;

                return Json(new { labels, sales = salesData, purchases = purchaseData, salesRangeTotal, purchasesRangeTotal, netRange });
            }
            catch (Exception ex)
            {
                return Json(new { error = true, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult SuperAdmin()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSuperAdminDashboard(string period = "ThisMonth", string? month = null)
        {
            try
            {
                var now = DateTime.Now;

                // Month filter: "all" | "current" | "previous" | "yyyy-MM"
                bool monthResolved = TryResolveMonth(month, now, out bool monthIsAll, out DateTime monthFrom, out DateTime monthTo);

                DateTime currentStart, currentEnd, prevStart, prevEnd;

                if (monthResolved && monthIsAll)
                {
                    // All time: wide range (prev same as current so trends stay neutral)
                    currentStart = new DateTime(2000, 1, 1);
                    currentEnd = now;
                    prevStart = currentStart;
                    prevEnd = currentEnd;
                }
                else if (monthResolved)
                {
                    currentStart = monthFrom;
                    currentEnd = monthFrom.AddMonths(1); // exclusive upper bound
                    prevStart = currentStart.AddMonths(-1);
                    prevEnd = currentStart;
                }
                else
                {
                    currentStart = new DateTime(now.Year, now.Month, 1);
                    currentEnd = currentStart.AddMonths(1);
                    prevStart = currentStart.AddMonths(-1);
                    prevEnd = currentStart;
                }

                // ============ 1. System KPIs (Global) ============
                // Super Admin (UserType == 1) is excluded from all user counts below.
                var adminUsers = await (from u in _dbContext.Users
                                         join ut in _dbContext.UserTypes on u.UserType equals ut.Id
                                         where ut.Name == "Admin" && u.UserType != 1
                                         select u).CountAsync();
                var authUsers = await (from u in _dbContext.Users
                                        join ut in _dbContext.UserTypes on u.UserType equals ut.Id
                                        where ut.Name == "Authorize"
                                        select u).CountAsync();
                var unAuthUsers = await (from u in _dbContext.Users
                                          join ut in _dbContext.UserTypes on u.UserType equals ut.Id
                                          where ut.Name == "UnAuthorize"
                                          select u).CountAsync();

                var systemKpis = new
                {
                    totalUsers = await _dbContext.Users.CountAsync(u => u.UserType != 1),
                    adminUsers,
                    authUsers,
                    unAuthUsers,
                    activeUsers = await _dbContext.Users.CountAsync(u => u.UserType != 1 && u.IsActive == true),
                    inactiveUsers = await _dbContext.Users.CountAsync(u => u.UserType != 1 && (u.IsActive == false || u.IsActive == null)),
                    totalCostCenters = await _dbContext.CostCenters.CountAsync(),
                    activeCostCenters = await _dbContext.CostCenters.CountAsync(c => c.IsActive == true),
                    totalCompanies = await _dbContext.Users
                        .Where(u => !string.IsNullOrEmpty(u.CompanyName))
                        .Select(u => u.CompanyName)
                        .Distinct()
                        .CountAsync(),
                    totalCustomers = await _dbContext.Customers.CountAsync(c => c.TypeId == 1),
                    totalServiceProviders = await _dbContext.Customers.CountAsync(c => c.TypeId == 2)
                };

                // ============ 2. Jobs Statistics (Global, No ReferenceId filter) ============
                static string FormatIntTrend(int current, int previous, out string cssClass)
                {
                    if (previous == 0)
                    {
                        cssClass = "trend-badge-success";
                        return current == 0 ? "0%" : "+100%";
                    }

                    double percent = Math.Round((current - previous) * 100.0 / previous, 1);

                    if (percent > 0)
                    {
                        cssClass = "trend-badge-success";
                        return $"+{percent}%";
                    }
                    if (percent < 0)
                    {
                        cssClass = "trend-badge-danger";
                        return $"{percent}%";
                    }

                    cssClass = "trend-badge-success";
                    return "0%";
                }

                int totalJobsCurrent = await _dbContext.JobImportMasters
                    .CountAsync(j => j.CreatedOn >= currentStart && j.CreatedOn < currentEnd);
                int totalJobsPrev = await _dbContext.JobImportMasters
                    .CountAsync(j => j.CreatedOn >= prevStart && j.CreatedOn < prevEnd);
                int importJobsCurrent = await _dbContext.JobImportMasters
                    .CountAsync(j => j.ShipmentType == 1 && j.CreatedOn >= currentStart && j.CreatedOn < currentEnd);
                int importJobsPrev = await _dbContext.JobImportMasters
                    .CountAsync(j => j.ShipmentType == 1 && j.CreatedOn >= prevStart && j.CreatedOn < prevEnd);
                int exportJobsCurrent = await _dbContext.JobImportMasters
                    .CountAsync(j => j.ShipmentType == 2 && j.CreatedOn >= currentStart && j.CreatedOn < currentEnd);
                int exportJobsPrev = await _dbContext.JobImportMasters
                    .CountAsync(j => j.ShipmentType == 2 && j.CreatedOn >= prevStart && j.CreatedOn < prevEnd);

                string totalJobsTrendClass, importJobsTrendClass, exportJobsTrendClass;
                var totalJobsTrendText = FormatIntTrend(totalJobsCurrent, totalJobsPrev, out totalJobsTrendClass);
                var importJobsTrendText = FormatIntTrend(importJobsCurrent, importJobsPrev, out importJobsTrendClass);
                var exportJobsTrendText = FormatIntTrend(exportJobsCurrent, exportJobsPrev, out exportJobsTrendClass);

                // ============ 3. Financial Statistics (Global) ============
                decimal salesCurrent = await _dbContext.SalesInvoices
                    .Where(x => (x.IsCancelled == null || x.IsCancelled == false)
                             && x.InvoiceDate >= currentStart && x.InvoiceDate < currentEnd)
                    .SumAsync(x => (decimal?)x.GrandTotal) ?? 0;
                decimal salesPrev = await _dbContext.SalesInvoices
                    .Where(x => (x.IsCancelled == null || x.IsCancelled == false)
                             && x.InvoiceDate >= prevStart && x.InvoiceDate < prevEnd)
                    .SumAsync(x => (decimal?)x.GrandTotal) ?? 0;

                decimal spOutstanding = await _dbContext.PurchaseInvoices
                    .Where(x => (x.IsCancelled == null || x.IsCancelled == false))
                    .SumAsync(x => (decimal?)((x.BalanceAmount == null || x.BalanceAmount == 0) ? x.GrandTotal : x.BalanceAmount.Value)) ?? 0;

                decimal purchaseCurrent = await _dbContext.PurchaseInvoices
                    .Where(x => (x.IsCancelled == null || x.IsCancelled == false)
                             && x.InvoiceDate >= currentStart && x.InvoiceDate < currentEnd)
                    .SumAsync(x => (decimal?)((x.BalanceAmount == null || x.BalanceAmount == 0) ? x.GrandTotal : x.BalanceAmount.Value)) ?? 0;
                decimal purchasePrev = await _dbContext.PurchaseInvoices
                    .Where(x => (x.IsCancelled == null || x.IsCancelled == false)
                             && x.InvoiceDate >= prevStart && x.InvoiceDate < prevEnd)
                    .SumAsync(x => (decimal?)((x.BalanceAmount == null || x.BalanceAmount == 0) ? x.GrandTotal : x.BalanceAmount.Value)) ?? 0;

                decimal receivedCurrent = await _dbContext.PaymentReceiveds
                    .Where(x => x.IsActive && x.PaymentDate >= currentStart && x.PaymentDate < currentEnd)
                    .SumAsync(x => (decimal?)x.Amount) ?? 0;
                decimal receivedPrev = await _dbContext.PaymentReceiveds
                    .Where(x => x.IsActive && x.PaymentDate >= prevStart && x.PaymentDate < prevEnd)
                    .SumAsync(x => (decimal?)x.Amount) ?? 0;
                decimal paidCurrent = await _dbContext.PurchaseVouchers
                    .Where(x => x.IsActive && x.PaymentDate >= currentStart && x.PaymentDate < currentEnd)
                    .SumAsync(x => (decimal?)x.Amount) ?? 0;
                decimal paidPrev = await _dbContext.PurchaseVouchers
                    .Where(x => x.IsActive && x.PaymentDate >= prevStart && x.PaymentDate < prevEnd)
                    .SumAsync(x => (decimal?)x.Amount) ?? 0;

                decimal netCashCurrent = receivedCurrent - paidCurrent;
                decimal netCashPrev = receivedPrev - paidPrev;

                static string FormatDecTrend(decimal current, decimal previous, out string cssClass)
                {
                    if (previous == 0)
                    {
                        cssClass = "trend-badge-success";
                        return current == 0 ? "0%" : "+100%";
                    }

                    double percent = Math.Round((double)((current - previous) * 100m / previous), 1);

                    if (percent > 0)
                    {
                        cssClass = "trend-badge-success";
                        return $"+{percent}%";
                    }
                    if (percent < 0)
                    {
                        cssClass = "trend-badge-danger";
                        return $"{percent}%";
                    }

                    cssClass = "trend-badge-success";
                    return "0%";
                }

                string salesTrendClass, spTrendClass, netCashTrendClass;
                var salesTrendText = FormatDecTrend(salesCurrent, salesPrev, out salesTrendClass);
                var spTrendText = FormatDecTrend(purchaseCurrent, purchasePrev, out spTrendClass);
                var netCashTrendText = FormatDecTrend(netCashCurrent, netCashPrev, out netCashTrendClass);

                // ============ 4. Recent Invoices (Global) ============
                DateTime? recentFrom = null, recentTo = null;
                if (monthResolved && !monthIsAll)
                {
                    recentFrom = monthFrom;
                    recentTo = monthTo;
                }

                var recentSalesQuery = from inv in _dbContext.SalesInvoices
                                       join cust in _dbContext.Customers on inv.CustomerId equals cust.Id into custJoin
                                       from cust in custJoin.DefaultIfEmpty()
                                       join job in _dbContext.JobImportMasters on inv.JobId equals job.Id into jobJoin
                                       from job in jobJoin.DefaultIfEmpty()
                                       where (inv.IsCancelled == null || inv.IsCancelled == false)
                                          && (!recentFrom.HasValue || inv.InvoiceDate >= recentFrom.Value)
                                          && (!recentTo.HasValue || inv.InvoiceDate <= recentTo.Value)
                                       select new
                                       {
                                           InvoiceDate = inv.InvoiceDate,
                                           Dto = new BusinessLogic.Models.RecentInvoiceDto
                                           {
                                               InvoiceNo = inv.InvoiceNo,
                                               JobNo = job.JobNumber,
                                               PartyName = cust.CustomerName,
                                               Type = "Sales",
                                               BLNumber = job.BlNo,
                                               GrandTotal = inv.GrandTotal,
                                               Status = inv.Status
                                           }
                                       };

                var recentPurchaseQuery = from inv in _dbContext.PurchaseInvoices
                                          join cust in _dbContext.Customers on inv.ServiceProviderId equals cust.Id into custJoin
                                          from cust in custJoin.DefaultIfEmpty()
                                          join job in _dbContext.JobImportMasters on inv.JobId equals job.Id into jobJoin
                                          from job in jobJoin.DefaultIfEmpty()
                                          where (inv.IsCancelled == null || inv.IsCancelled == false)
                                             && (!recentFrom.HasValue || inv.InvoiceDate >= recentFrom.Value)
                                             && (!recentTo.HasValue || inv.InvoiceDate <= recentTo.Value)
                                          select new
                                          {
                                              InvoiceDate = inv.InvoiceDate,
                                              Dto = new BusinessLogic.Models.RecentInvoiceDto
                                              {
                                                  InvoiceNo = inv.InvoiceNo,
                                                  JobNo = job.JobNumber,
                                                  PartyName = cust.CustomerName ?? inv.ServiceProviderName,
                                                  Type = "Purchase",
                                                  BLNumber = job.BlNo,
                                                  GrandTotal = inv.GrandTotal,
                                                  Status = inv.Status
                                              }
                                          };

                var recentInvoices = await recentSalesQuery.Concat(recentPurchaseQuery)
                                       .OrderByDescending(x => x.InvoiceDate)
                                       .Take(8)
                                       .Select(x => x.Dto)
                                       .ToListAsync();

                // ============ 5. Job Operations Share (Global) ============
                DateTime opsFrom, opsTo;
                if (monthResolved && monthIsAll)
                {
                    opsFrom = new DateTime(2000, 1, 1);
                    opsTo = now;
                }
                else if (monthResolved)
                {
                    opsFrom = monthFrom;
                    opsTo = monthTo;
                }
                else if (period == "LastMonth")
                {
                    opsFrom = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                    opsTo = new DateTime(now.Year, now.Month, 1);
                }
                else if (period == "ThisYear")
                {
                    opsFrom = new DateTime(now.Year, 1, 1);
                    opsTo = now;
                }
                else
                {
                    opsFrom = currentStart;
                    opsTo = now;
                }

                var opsData = await (from job in _dbContext.JobImportMasters
                                     join type in _dbContext.JobTypes
                                          on job.JobTypeId equals type.Id into typeJoin
                                     from type in typeJoin.DefaultIfEmpty()
                                     where job.CreatedOn >= opsFrom && job.CreatedOn <= opsTo
                                     group job by (type != null ? type.Name : "Unknown") into g
                                     select new
                                     {
                                         Label = g.Key,
                                         Count = g.Count()
                                     })
                                     .ToListAsync();

                int opsTotal = opsData.Sum(x => x.Count);
                if (opsTotal == 0) opsTotal = 1;

                var jobOperationsShare = opsData.Select(x => new BusinessLogic.Models.JobOperationsShareDto
                {
                    Label = x.Label,
                    Count = x.Count,
                    Percentage = Math.Round((decimal)x.Count * 100m / opsTotal, 1)
                }).OrderByDescending(x => x.Count).ToList();

                // ============ 6. Users by Type (Donut) ============
                // Super Admin (UserType == 1) is excluded from this breakdown.
                var usersByType = await (from u in _dbContext.Users
                                         join ut in _dbContext.UserTypes on u.UserType equals ut.Id into tJoin
                                         from ut in tJoin.DefaultIfEmpty()
                                         where u.UserType != 1
                                         group u by (ut != null ? ut.Name : "Unknown") into g
                                         select new
                                         {
                                             Label = g.Key,
                                             Count = g.Count()
                                         })
                                         .OrderByDescending(x => x.Count)
                                         .ToListAsync();

                // ============ 7. Recent Users ============
                var recentUsers = await (from u in _dbContext.Users
                                         join ut in _dbContext.UserTypes on u.UserType equals ut.Id into tJoin
                                         from ut in tJoin.DefaultIfEmpty()
                                         orderby u.CreatedOn descending
                                         select new
                                         {
                                             u.Id,
                                             u.UserId,
                                             u.UserName,
                                             TypeName = ut != null ? ut.Name : "-",
                                             u.IsActive,
                                             u.CreatedOn
                                         })
                                         .Take(8)
                                         .ToListAsync();

                return Json(new
                {
                    systemKpis,
                    jobsStatistics = new
                    {
                        totalJobs = totalJobsCurrent,
                        importJobs = importJobsCurrent,
                        exportJobs = exportJobsCurrent,
                        totalJobsTrendText,
                        totalJobsTrendClass,
                        importJobsTrendText,
                        importJobsTrendClass,
                        exportJobsTrendText,
                        exportJobsTrendClass
                    },
                    financialStatistics = new
                    {
                        salesRevenue = salesCurrent,
                        salesTrendText,
                        salesTrendClass,
                        spOutstanding,
                        spTrendText,
                        spTrendClass,
                        netCashInflow = netCashCurrent,
                        netCashTrendText,
                        netCashTrendClass
                    },
                    recentInvoices,
                    jobOperationsShare,
                    usersByType,
                    recentUsers
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = true, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetGlobalSalesPurchaseTrend(string period = "ThisYear", string? from = null, string? to = null, string? month = null)
        {
            try
            {
                var now = DateTime.Now;
                DateTime fromDate, toDate;

                if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                {
                    fromDate = DateTime.Parse(from);
                    toDate = DateTime.Parse(to);
                }
                else if (TryResolveMonth(month, now, out bool gMonthIsAll, out DateTime gMonthFrom, out DateTime gMonthTo))
                {
                    if (gMonthIsAll)
                    {
                        // All time: start from the earliest invoice available
                        var minSales = await _dbContext.SalesInvoices
                            .Where(x => (x.IsCancelled == null || x.IsCancelled == false))
                            .MinAsync(x => (DateTime?)x.InvoiceDate);

                        var minPurchases = await _dbContext.PurchaseInvoices
                            .Where(x => (x.IsCancelled == null || x.IsCancelled == false))
                            .MinAsync(x => (DateTime?)x.InvoiceDate);

                        DateTime? minDate = minSales;
                        if (minPurchases.HasValue && (!minDate.HasValue || minPurchases < minDate))
                            minDate = minPurchases;

                        fromDate = minDate.HasValue
                            ? new DateTime(minDate.Value.Year, minDate.Value.Month, 1)
                            : new DateTime(now.Year, 1, 1);
                        toDate = now;
                    }
                    else
                    {
                        fromDate = gMonthFrom;
                        toDate = gMonthTo;
                    }
                }
                else
                {
                    if (period == "LastMonth")
                    {
                        fromDate = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                        toDate = fromDate.AddMonths(1).AddDays(-1);
                    }
                    else if (period == "ThisMonth")
                    {
                        fromDate = new DateTime(now.Year, now.Month, 1);
                        toDate = now;
                    }
                    else if (period == "LastYear")
                    {
                        fromDate = new DateTime(now.Year - 1, 1, 1);
                        toDate = new DateTime(now.Year - 1, 12, 31);
                    }
                    else
                    {
                        fromDate = new DateTime(now.Year, 1, 1);
                        toDate = now;
                    }
                }

                var salesRaw = await _dbContext.SalesInvoices
                    .Where(x => x.InvoiceDate >= fromDate && x.InvoiceDate <= toDate
                             && (x.IsCancelled == null || x.IsCancelled == false))
                    .Select(x => new { x.InvoiceDate, x.GrandTotal })
                    .ToListAsync();

                var purchasesRaw = await _dbContext.PurchaseInvoices
                    .Where(x => x.InvoiceDate >= fromDate && x.InvoiceDate <= toDate
                             && (x.IsCancelled == null || x.IsCancelled == false))
                    .Select(x => new {
                        x.InvoiceDate,
                        Amount = (x.BalanceAmount == null || x.BalanceAmount == 0) ? x.GrandTotal : x.BalanceAmount.Value
                    })
                    .ToListAsync();

                var labels = new List<string>();
                var salesData = new List<decimal>();
                var purchaseData = new List<decimal>();

                var cursor = new DateTime(fromDate.Year, fromDate.Month, 1);
                var endCursor = new DateTime(toDate.Year, toDate.Month, 1);

                while (cursor <= endCursor)
                {
                    var monthStart = cursor;
                    var monthEnd = cursor.AddMonths(1);

                    decimal salesSum = salesRaw
                        .Where(x => x.InvoiceDate >= monthStart && x.InvoiceDate < monthEnd)
                        .Sum(x => x.GrandTotal);

                    decimal purchaseSum = purchasesRaw
                        .Where(x => x.InvoiceDate >= monthStart && x.InvoiceDate < monthEnd)
                        .Sum(x => x.Amount);

                    labels.Add(cursor.ToString("MMM yyyy"));
                    salesData.Add(salesSum);
                    purchaseData.Add(purchaseSum);

                    cursor = cursor.AddMonths(1);
                }

                decimal salesRangeTotal = salesRaw.Sum(x => x.GrandTotal);
                decimal purchasesRangeTotal = purchasesRaw.Sum(x => x.Amount);
                decimal netRange = salesRangeTotal - purchasesRangeTotal;

                return Json(new { labels, sales = salesData, purchases = purchaseData, salesRangeTotal, purchasesRangeTotal, netRange });
            }
            catch (Exception ex)
            {
                return Json(new { error = true, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetRevenueBillingTrend(string period = "ThisYear", string? from = null, string? to = null, string? month = null)
        {
            try
            {
                var refId = _session.ReferenceId;
                var now = DateTime.Now;
                DateTime fromDate, toDate;

                if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                {
                    fromDate = DateTime.Parse(from);
                    toDate = DateTime.Parse(to);
                }
                else if (TryResolveMonth(month, now, out bool revMonthIsAll, out DateTime revMonthFrom, out DateTime revMonthTo))
                {
                    if (revMonthIsAll)
                    {
                        // All time: start from the earliest invoice / payment available
                        var minInvoiced = await _dbContext.SalesInvoices
                            .Where(x => x.ReferenceId == refId
                                     && (x.IsCancelled == null || x.IsCancelled == false))
                            .MinAsync(x => (DateTime?)x.InvoiceDate);

                        var minCollected = await _dbContext.PaymentReceiveds
                            .Where(x => x.ReferenceId == refId && x.IsActive)
                            .MinAsync(x => (DateTime?)x.PaymentDate);

                        DateTime? minDate = minInvoiced;
                        if (minCollected.HasValue && (!minDate.HasValue || minCollected < minDate))
                            minDate = minCollected;

                        fromDate = minDate.HasValue
                            ? new DateTime(minDate.Value.Year, minDate.Value.Month, 1)
                            : new DateTime(now.Year, 1, 1);
                        toDate = now;
                    }
                    else
                    {
                        fromDate = revMonthFrom;
                        toDate = revMonthTo;
                    }
                }
                else
                {
                    if (period == "LastMonth")
                    {
                        fromDate = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                        toDate = fromDate.AddMonths(1).AddDays(-1);
                    }
                    else if (period == "ThisMonth")
                    {
                        fromDate = new DateTime(now.Year, now.Month, 1);
                        toDate = now;
                    }
                    else if (period == "LastYear")
                    {
                        fromDate = new DateTime(now.Year - 1, 1, 1);
                        toDate = new DateTime(now.Year - 1, 12, 31);
                    }
                    else // ThisYear
                    {
                        fromDate = new DateTime(now.Year, 1, 1);
                        toDate = now;
                    }
                }

                // 1. Fetch Invoiced Raw Data for Entire Range (Single DB Query)
                var invoicedRaw = await _dbContext.SalesInvoices
                    .Where(x => x.ReferenceId == refId
                             && x.InvoiceDate >= fromDate && x.InvoiceDate <= toDate
                             && (x.IsCancelled == null || x.IsCancelled == false))
                    .Select(x => new { x.InvoiceDate, x.GrandTotal })
                    .ToListAsync();

                // 2. Fetch Collected Raw Data for Entire Range (Single DB Query)
                var collectedRaw = await _dbContext.PaymentReceiveds
                    .Where(x => x.ReferenceId == refId
                             && x.PaymentDate >= fromDate && x.PaymentDate <= toDate
                             && x.IsActive)
                    .Select(x => new { x.PaymentDate, x.Amount })
                    .ToListAsync();

                var labels = new List<string>();
                var invoiced = new List<decimal>();
                var collected = new List<decimal>();

                var cursor = new DateTime(fromDate.Year, fromDate.Month, 1);
                var endCursor = new DateTime(toDate.Year, toDate.Month, 1);

                while (cursor <= endCursor)
                {
                    var monthStart = cursor;
                    var monthEnd = cursor.AddMonths(1);

                    decimal invoicedSum = invoicedRaw
                        .Where(x => x.InvoiceDate >= monthStart && x.InvoiceDate < monthEnd)
                        .Sum(x => x.GrandTotal);

                    decimal collectedSum = collectedRaw
                        .Where(x => x.PaymentDate >= monthStart && x.PaymentDate < monthEnd)
                        .Sum(x => x.Amount);

                    labels.Add(cursor.ToString("MMM yyyy"));
                    invoiced.Add(invoicedSum);
                    collected.Add(collectedSum);

                    cursor = cursor.AddMonths(1);
                }

                decimal totalInvoiced = invoiced.Sum();
                decimal totalCollected = collected.Sum();
                decimal remaining = totalInvoiced - totalCollected;

                return Json(new { labels, invoiced, collected, totalInvoiced, totalCollected, remaining });
            }
            catch (Exception ex)
            {
                return Json(new { error = true, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetGlobalRevenueBillingTrend(string period = "ThisYear", string? from = null, string? to = null, string? month = null)
        {
            try
            {
                var now = DateTime.Now;
                DateTime fromDate, toDate;

                if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                {
                    fromDate = DateTime.Parse(from);
                    toDate = DateTime.Parse(to);
                }
                else if (TryResolveMonth(month, now, out bool grMonthIsAll, out DateTime grMonthFrom, out DateTime grMonthTo))
                {
                    if (grMonthIsAll)
                    {
                        // All time: start from the earliest invoice / payment available
                        var minInvoiced = await _dbContext.SalesInvoices
                            .Where(x => (x.IsCancelled == null || x.IsCancelled == false))
                            .MinAsync(x => (DateTime?)x.InvoiceDate);

                        var minCollected = await _dbContext.PaymentReceiveds
                            .Where(x => x.IsActive)
                            .MinAsync(x => (DateTime?)x.PaymentDate);

                        DateTime? minDate = minInvoiced;
                        if (minCollected.HasValue && (!minDate.HasValue || minCollected < minDate))
                            minDate = minCollected;

                        fromDate = minDate.HasValue
                            ? new DateTime(minDate.Value.Year, minDate.Value.Month, 1)
                            : new DateTime(now.Year, 1, 1);
                        toDate = now;
                    }
                    else
                    {
                        fromDate = grMonthFrom;
                        toDate = grMonthTo;
                    }
                }
                else
                {
                    if (period == "LastMonth")
                    {
                        fromDate = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                        toDate = fromDate.AddMonths(1).AddDays(-1);
                    }
                    else if (period == "ThisMonth")
                    {
                        fromDate = new DateTime(now.Year, now.Month, 1);
                        toDate = now;
                    }
                    else if (period == "LastYear")
                    {
                        fromDate = new DateTime(now.Year - 1, 1, 1);
                        toDate = new DateTime(now.Year - 1, 12, 31);
                    }
                    else // ThisYear
                    {
                        fromDate = new DateTime(now.Year, 1, 1);
                        toDate = now;
                    }
                }

                // 1. Fetch Invoiced Raw Data for Entire Range (Single DB Query)
                var invoicedRaw = await _dbContext.SalesInvoices
                    .Where(x => x.InvoiceDate >= fromDate && x.InvoiceDate <= toDate
                             && (x.IsCancelled == null || x.IsCancelled == false))
                    .Select(x => new { x.InvoiceDate, x.GrandTotal })
                    .ToListAsync();

                // 2. Fetch Collected Raw Data for Entire Range (Single DB Query)
                var collectedRaw = await _dbContext.PaymentReceiveds
                    .Where(x => x.PaymentDate >= fromDate && x.PaymentDate <= toDate
                             && x.IsActive)
                    .Select(x => new { x.PaymentDate, x.Amount })
                    .ToListAsync();

                var labels = new List<string>();
                var invoiced = new List<decimal>();
                var collected = new List<decimal>();

                var cursor = new DateTime(fromDate.Year, fromDate.Month, 1);
                var endCursor = new DateTime(toDate.Year, toDate.Month, 1);

                // Grouping data in-memory per month
                while (cursor <= endCursor)
                {
                    var monthStart = cursor;
                    var monthEnd = cursor.AddMonths(1);

                    decimal invoicedSum = invoicedRaw
                        .Where(x => x.InvoiceDate >= monthStart && x.InvoiceDate < monthEnd)
                        .Sum(x => x.GrandTotal);

                    decimal collectedSum = collectedRaw
                        .Where(x => x.PaymentDate >= monthStart && x.PaymentDate < monthEnd)
                        .Sum(x => x.Amount);

                    labels.Add(cursor.ToString("MMM yyyy"));
                    invoiced.Add(invoicedSum);
                    collected.Add(collectedSum);

                    cursor = cursor.AddMonths(1);
                }

                // Totals for the requested period
                decimal totalInvoiced = invoiced.Sum();
                decimal totalCollected = collected.Sum();
                decimal remaining = totalInvoiced - totalCollected;

                return Json(new { labels, invoiced, collected, totalInvoiced, totalCollected, remaining });
            }
            catch (Exception ex)
            {
                return Json(new { error = true, message = ex.Message });
            }
        }
    }
}