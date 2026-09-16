using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using Microsoft.EntityFrameworkCore;
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
        public async Task<IActionResult> GetDashboardData(string period = "ThisMonth")
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
                var recentInvoices = await _jobImportMaster.GetRecentInvoicesAsync(8);

                // ==================== 3. Job Operations Share ====================
                var jobOperationsShare = await _jobImportMaster.GetJobOperationsShareAsync(period);

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
        public async Task<IActionResult> GetSalesPurchaseTrend(string period = "ThisYear", string? from = null, string? to = null)
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
                    var monthEnd = cursor.AddMonths(1).AddDays(-1);

                    decimal salesSum = salesRaw
                        .Where(x => x.InvoiceDate >= monthStart && x.InvoiceDate <= monthEnd)
                        .Sum(x => x.GrandTotal);

                    decimal purchaseSum = purchasesRaw
                        .Where(x => x.InvoiceDate >= monthStart && x.InvoiceDate <= monthEnd)
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
        public async Task<IActionResult> GetRevenueBillingTrend(string period = "ThisYear", string? from = null, string? to = null)
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

                // Grouping data in-memory per month
                while (cursor <= endCursor)
                {
                    var monthStart = cursor;
                    var monthEnd = cursor.AddMonths(1).AddDays(-1);

                    decimal invoicedSum = invoicedRaw
                        .Where(x => x.InvoiceDate >= monthStart && x.InvoiceDate <= monthEnd)
                        .Sum(x => x.GrandTotal);

                    decimal collectedSum = collectedRaw
                        .Where(x => x.PaymentDate >= monthStart && x.PaymentDate <= monthEnd)
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