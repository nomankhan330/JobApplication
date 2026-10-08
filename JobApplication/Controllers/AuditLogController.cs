using System.Text.RegularExpressions;
using System.Globalization;
using BusinessLogic.Helper;
using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JobApplication.Controllers
{
    public class AuditLogController : BaseController
    {
        private readonly AppDbContext _db;
        private readonly ISessionHelper _session;
        private readonly IAuditService _auditService;
        private readonly ILogs _logs;

        public AuditLogController(AppDbContext db, ISessionHelper session, IAuditService auditService, ILogs logs) : base(session)
        {
            _db = db;
            _session = session;
            _auditService = auditService;
            _logs = logs;
        }

        [HttpGet("/AuditLog")]
        public IActionResult Index()
        {
            if (_session.UserType != 1)
            {
                return RedirectToAction("Unauthorized", "Job");
            }

            return View();
        }

        /// <summary>
        /// DataTables server-side endpoint for the audit trail grid.
        /// Supports free-text search plus optional Module / Action / User / date filters.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GetItems()
        {
            if (_session.UserType != 1)
            {
                return Json(new { draw = 0, recordsTotal = 0, recordsFiltered = 0, data = Array.Empty<object>() });
            }

            int draw = 0, start = 0, length = 25;
            string search = "";

            if (Request.HasFormContentType)
            {
                draw = ParseInt(Request.Form["draw"].FirstOrDefault(), 0);
                start = ParseInt(Request.Form["start"].FirstOrDefault(), 0);
                length = ParseInt(Request.Form["length"].FirstOrDefault(), 25);
                search = (Request.Form["search[value]"].FirstOrDefault() ?? "").Trim();
            }

            if (length <= 0 || length > 500) length = 25;

            var query = _db.AuditLogs.AsNoTracking();

            int recordsTotal = await query.CountAsync();

            // ---- Optional filters (passed as extra datatables form fields) ----
            if (Request.HasFormContentType)
            {
                string moduleFilter = (Request.Form["module"].FirstOrDefault() ?? "").Trim();
                string actionFilter = (Request.Form["action"].FirstOrDefault() ?? "").Trim();
                string userFilter = (Request.Form["userName"].FirstOrDefault() ?? "").Trim();
                string entityFilter = (Request.Form["entityId"].FirstOrDefault() ?? "").Trim();
                string fromFilter = (Request.Form["fromDate"].FirstOrDefault() ?? "").Trim();
                string toFilter = (Request.Form["toDate"].FirstOrDefault() ?? "").Trim();

                if (!string.IsNullOrEmpty(moduleFilter))
                    query = query.Where(x => x.Module != null && x.Module.Equals(moduleFilter, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(actionFilter))
                    query = query.Where(x => x.Action != null && x.Action.Equals(actionFilter, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(userFilter))
                    query = query.Where(x => x.UserName != null && x.UserName.Contains(userFilter, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(entityFilter))
                    query = query.Where(x => x.EntityId != null && x.EntityId.Contains(entityFilter, StringComparison.OrdinalIgnoreCase));

                if (DateTime.TryParse(fromFilter, CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) ||
                    DateTime.TryParse(fromFilter, CultureInfo.CurrentCulture, DateTimeStyles.None, out from) ||
                    DateTime.TryParseExact(fromFilter, new[] { "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy", "dd/MM/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out from))
                    query = query.Where(x => x.AuditDate >= from);

                if (DateTime.TryParse(toFilter, CultureInfo.InvariantCulture, DateTimeStyles.None, out var to) ||
                    DateTime.TryParse(toFilter, CultureInfo.CurrentCulture, DateTimeStyles.None, out to) ||
                    DateTime.TryParseExact(toFilter, new[] { "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy", "dd/MM/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out to))
                    query = query.Where(x => x.AuditDate < to.AddDays(1));
            }

            // ---- Global search ----
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x =>
                    (x.Description != null && x.Description.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (x.EntityId != null && x.EntityId.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (x.UserName != null && x.UserName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Module != null && x.Module.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Action != null && x.Action.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (x.IpAddress != null && x.IpAddress.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (x.PageName != null && x.PageName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (x.EntityName != null && x.EntityName.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            int recordsFiltered = await query.CountAsync();

            var rows = await query
                .OrderByDescending(x => x.Id)
                .Skip(start)
                .Take(length)
                .ToListAsync();

            // ---- Resolve customer / job ids to readable names for the detail popup ----
            var nameMap = await BuildNameMapAsync(rows);

            var data = rows.Select(x => new
            {
                x.Id,
                AuditDate = x.AuditDate.ToString("yyyy-MM-dd HH:mm:ss"),
                x.UserName,
                x.UserType,
                x.Module,
                x.Action,
                x.EntityName,
                x.EntityId,
                Description = ResolveDescription(x.Description, nameMap),
                x.OldValues,
                x.NewValues,
                x.IpAddress,
                x.PageName,
                Names = BuildRowNames(x, nameMap)
            })
            .ToList();

            return Json(new { draw, recordsTotal, recordsFiltered, data });
        }

        /// <summary>
        /// Rewrites historic "customer #12" / "job #7" placeholders into real names on
        /// read, so rows logged before names were stored still read properly.
        /// </summary>
        private static string ResolveDescription(string? description, AuditNameMap map)
        {
            if (string.IsNullOrWhiteSpace(description)) return description ?? string.Empty;

            string Replace(string input, string pattern, string keyPrefix)
            {
                return Regex.Replace(
                    input,
                    pattern,
                    m => map.Lookup.TryGetValue($"{keyPrefix}{m.Groups[1].Value}", out var name)
                        ? name
                        : m.Value);
            }

            description = Replace(description, @"\bcustomer: #(\d+)", "CustomerId:");
            description = Replace(description, @"\bcustomer #(\d+)", "CustomerId:");
            description = Replace(description, @"\bservice provider #(\d+)", "CustomerId:");
            description = Replace(description, @"\bjob #(\d+)", "JobId:");

            // Historic rows logged "status 2 -> 2"; show the readable label instead.
            description = Regex.Replace(
                description,
                @"\bstatus (\d+) -> (\d+)\b",
                m => $"status {StatusHelper.GetInvoiceStatus(int.Parse(m.Groups[1].Value))}" +
                     $" -> {StatusHelper.GetInvoiceStatus(int.Parse(m.Groups[2].Value))}");

            return description;
        }

        /// <summary>
        /// Collects every customer / service-provider / job id referenced by the change
        /// payloads on this page and resolves them in two batched queries.
        /// </summary>
        private async Task<AuditNameMap> BuildNameMapAsync(List<AuditLog> rows)
        {
            var map = new AuditNameMap();

            var customerIds = new HashSet<int>();
            var jobIds = new HashSet<int>();

            foreach (var row in rows)
            {
                CollectIds(row.OldValues, customerIds, jobIds);
                CollectIds(row.NewValues, customerIds, jobIds);
            }

            if (customerIds.Count == 0 && jobIds.Count == 0) return map;

            try
            {
                var customers = await _db.Customers
                    .AsNoTracking()
                    .Where(c => customerIds.Contains(c.Id))
                    .Select(c => new { c.Id, c.CustomerName, c.CustomerCode })
                    .ToListAsync();

                foreach (var c in customers)
                {
                    map.AddCustomer(c.Id, c.CustomerName ?? c.CustomerCode);
                }

                var jobs = await _db.JobImportMasters
                    .AsNoTracking()
                    .Where(j => jobIds.Contains(j.Id))
                    .Select(j => new { j.Id, j.JobNumber })
                    .ToListAsync();

                foreach (var j in jobs)
                {
                    map.AddJob(j.Id, j.JobNumber);
                }
            }
            catch
            {
                // A name lookup failure must not break the grid - fall back to raw ids.
            }

            return map;
        }

        private static void CollectIds(string? json, HashSet<int> customerIds, HashSet<int> jobIds)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            JObject payload;
            try
            {
                payload = JObject.Parse(json);
            }
            catch
            {
                return;
            }

            foreach (var prop in payload.Properties())
            {
                if (prop.Value == null || prop.Value.Type == JTokenType.Null) continue;

                if (!int.TryParse(prop.Value.ToString(), out int id) || id <= 0) continue;

                switch (prop.Name)
                {
                    case "CustomerId":
                    case "ServiceProviderId":
                        customerIds.Add(id);
                        break;
                    case "JobId":
                        jobIds.Add(id);
                        break;
                }
            }
        }

        /// <summary>Only the ids actually present in this row's before/after payloads.</summary>
        private static Dictionary<string, string> BuildRowNames(AuditLog row, AuditNameMap map)
        {
            var result = new Dictionary<string, string>();

            foreach (var json in new[] { row.OldValues, row.NewValues })
            {
                if (string.IsNullOrWhiteSpace(json)) continue;

                JObject payload;
                try
                {
                    payload = JObject.Parse(json);
                }
                catch
                {
                    continue;
                }

                foreach (var prop in payload.Properties())
                {
                    var name = map.Resolve(prop.Name, prop.Value?.ToString());
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        result[$"{prop.Name}:{prop.Value}"] = name!;
                    }
                }
            }

            return result;
        }

        /// <summary>Distinct modules and actions, used to populate the filter dropdowns.</summary>
        [HttpPost]
        public async Task<IActionResult> GetFilterOptions()
        {
            if (_session.UserType != 1)
            {
                return Json(new { success = false, modules = Array.Empty<string>(), actions = Array.Empty<string>() });
            }

            var modules = await _db.AuditLogs.AsNoTracking()
                .Where(x => x.Module != null)
                .Select(x => x.Module!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            var actions = await _db.AuditLogs.AsNoTracking()
                .Where(x => x.Action != null)
                .Select(x => x.Action!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            return Json(new { success = true, modules, actions });
        }

        private static int ParseInt(string? value, int fallback)
        {
            return int.TryParse(value, out int result) ? result : fallback;
        }

        [HttpGet]
        public async Task<IActionResult> DownloadExcel(string module, string auditAction, string userName, string entityId, string fromDate, string toDate, string search = "")
        {
            if (_session.UserType != 1)
            {
                return RedirectToAction("Unauthorized", "Job");
            }

            var filters = new List<QueryFilters>
            {
                new QueryFilters { fieldName = "Module", filterValue = module },
                new QueryFilters { fieldName = "Action", filterValue = auditAction },
                new QueryFilters { fieldName = "UserName", filterValue = userName },
                new QueryFilters { fieldName = "EntityId", filterValue = entityId },
                new QueryFilters { fieldName = "FromDate", filterValue = fromDate },
                new QueryFilters { fieldName = "ToDate", filterValue = toDate },
                new QueryFilters { fieldName = "search", filterValue = search }
            };

            _logs.Write("AuditLog", "DownloadExcel", $"module='{module}' auditAction='{auditAction}' user='{userName}' entity='{entityId}' from='{fromDate}' to='{toDate}' search='{search}'");

            byte[] fileBytes;
            try
            {
                fileBytes = await _auditService.ExportExcel(filters);
            }
            catch (Exception ex)
            {
                _logs.Write("AuditLog", "DownloadExcel-Err", ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "Excel export failed: " + ex.Message);
            }

            string fileName = $"AuditLogs_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadPdf(string module, string auditAction, string userName, string entityId, string fromDate, string toDate, string search = "")
        {
            if (_session.UserType != 1)
            {
                return RedirectToAction("Unauthorized", "Job");
            }

            var filters = new List<QueryFilters>
            {
                new QueryFilters { fieldName = "Module", filterValue = module },
                new QueryFilters { fieldName = "Action", filterValue = auditAction },
                new QueryFilters { fieldName = "UserName", filterValue = userName },
                new QueryFilters { fieldName = "EntityId", filterValue = entityId },
                new QueryFilters { fieldName = "FromDate", filterValue = fromDate },
                new QueryFilters { fieldName = "ToDate", filterValue = toDate },
                new QueryFilters { fieldName = "search", filterValue = search }
            };

            _logs.Write("AuditLog", "DownloadPdf", $"module='{module}' auditAction='{auditAction}' user='{userName}' entity='{entityId}' from='{fromDate}' to='{toDate}' search='{search}'");

            byte[] fileBytes;
            try
            {
                fileBytes = await _auditService.ExportPdf(filters);
            }
            catch (Exception ex)
            {
                _logs.Write("AuditLog", "DownloadPdf-Err", ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "PDF export failed: " + ex.Message);
            }

            string fileName = $"AuditLogs_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

            return File(fileBytes, "application/pdf", fileName);
        }
    }
}
