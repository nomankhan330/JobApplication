using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using ClosedXML.Excel;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Services
{
    public class AuditService : IAuditService
    {
        private const int MaxStringLength = 500;

        /// <summary>
        /// Audit/technical columns that carry no business meaning - excluded so the
        /// diff only shows fields a user actually changed.
        /// </summary>
        private static readonly HashSet<string> ExcludedProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            "CreatedBy", "CreatedOn", "ModifiedBy", "ModifiedOn",
            "ReferenceId", "CancelledBy", "CancelledOn", "CancBy", "CancOn",
            "StatusName", "PaymentTypeName", "PaymentHeaderName"
        };

        private readonly AppDbContext _context;
        private readonly ISessionHelper _session;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogs _logs;
        private readonly IConverter _converter;

        public AuditService(
            AppDbContext context,
            ISessionHelper session,
            IHttpContextAccessor httpContextAccessor,
            ILogs logs,
            IConverter converter)
        {
            _context = context;
            _session = session;
            _httpContextAccessor = httpContextAccessor;
            _logs = logs;
            _converter = converter;
        }

        // =============================================================
        // PUBLIC API
        // =============================================================

        public IDictionary<string, object?> Snapshot(object? entity)
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            if (entity == null) return result;

            if (entity is IDictionary source)
            {
                foreach (DictionaryEntry entry in source)
                {
                    string key = Convert.ToString(entry.Key) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(key) || ExcludedProperties.Contains(key)) continue;
                    result[key] = Normalize(entry.Value);
                }

                return result;
            }

            foreach (var prop in entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead) continue;
                if (prop.GetIndexParameters().Length > 0) continue;
                if (ExcludedProperties.Contains(prop.Name)) continue;
                if (!IsScalar(prop.PropertyType)) continue;

                object? value;
                try
                {
                    value = prop.GetValue(entity);
                }
                catch
                {
                    // Lazy-loaded / throwing property - skip it.
                    continue;
                }

                result[prop.Name] = Normalize(value);
            }

            return result;
        }

        public async Task<AuditNameMap> ResolveNamesAsync(int? customerId = null, int? jobId = null)
        {
            var map = new AuditNameMap();

            try
            {
                if (customerId.HasValue && customerId.Value > 0)
                {
                    // Service providers live in the same Customers table (TypeId == 2),
                    // so one lookup covers both.
                    var customer = await _context.Customers
                        .AsNoTracking()
                        .Where(c => c.Id == customerId.Value)
                        .Select(c => new { c.CustomerName, c.CustomerCode })
                        .FirstOrDefaultAsync();

                    map.AddCustomer(customerId.Value, customer?.CustomerName ?? customer?.CustomerCode);
                }

                if (jobId.HasValue && jobId.Value > 0)
                {
                    var job = await _context.JobImportMasters
                        .AsNoTracking()
                        .Where(j => j.Id == jobId.Value)
                        .Select(j => new { j.JobNumber })
                        .FirstOrDefaultAsync();

                    map.AddJob(jobId.Value, job?.JobNumber);
                }
            }
            catch (Exception ex)
            {
                try { _logs?.Write("Audit", "ResolveNamesAsync", ex.ToString()); }
                catch { /* logging failed too */ }
            }

            return map;
        }

        public async Task RecordAsync(AuditEntryRequest request)
        {
            if (request == null) return;

            AuditLog? entry = null;

            try
            {
                var (oldJson, newJson) = BuildDiff(request.OldValues, request.NewValues);

                // Nothing actually changed - do not pollute the log.
                if (oldJson == null && newJson == null && request.OldValues != null) return;

                var httpContext = _httpContextAccessor?.HttpContext;

                entry = new AuditLog
                {
                    AuditDate = DateTime.Now,
                    UserId = request.UserIdOverride ?? (_session?.LoginId > 0 ? _session.LoginId : null),
                    UserName = Truncate(request.UserNameOverride ?? _session?.UserName, 100),
                    UserType = _session?.UserType,
                    ReferenceId = _session?.ReferenceId,
                    Module = Truncate(request.Module, 100),
                    Action = Truncate(request.Action, 50),
                    EntityName = Truncate(request.EntityName, 100),
                    EntityId = Truncate(request.EntityId, 200),
                    Description = Truncate(request.Description, 1000),
                    OldValues = oldJson,
                    NewValues = newJson,
                    IpAddress = ResolveIpAddress(httpContext),
                    UserAgent = Truncate(httpContext?.Request?.Headers["User-Agent"].FirstOrDefault(), 500),
                    PageName = Truncate(request.PageName, 200)
                };

                _context.AuditLogs.Add(entry);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Never let a failing audit write break the business operation.
                // Detach so a later SaveChanges does not retry the broken insert.
                if (entry != null)
                {
                    try { _context.Entry(entry).State = EntityState.Detached; }
                    catch { /* context may already be unusable */ }
                }

                try { _logs?.Write("Audit", "RecordAsync", ex.ToString()); }
                catch { /* logging failed too - nothing else we can do */ }
            }
        }

        // =============================================================
        // DIFF
        // =============================================================

        private (string? OldJson, string? NewJson) BuildDiff(
            IDictionary<string, object?>? oldValues,
            IDictionary<string, object?>? newValues)
        {
            // Create: no before-state, store the full after-state.
            if (oldValues == null || oldValues.Count == 0)
            {
                if (newValues == null || newValues.Count == 0) return (null, null);
                return (null, Serialize(newValues));
            }

            // Delete: no after-state, store the full before-state.
            if (newValues == null || newValues.Count == 0)
            {
                return (Serialize(oldValues), null);
            }

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in oldValues.Keys) keys.Add(key);
            foreach (var key in newValues.Keys) keys.Add(key);

            var changedOld = new Dictionary<string, object?>();
            var changedNew = new Dictionary<string, object?>();

            foreach (var key in keys)
            {
                oldValues.TryGetValue(key, out var oldValue);
                newValues.TryGetValue(key, out var newValue);

                if (AreEqual(oldValue, newValue)) continue;

                changedOld[key] = oldValue;
                changedNew[key] = newValue;
            }

            if (changedOld.Count == 0 && changedNew.Count == 0) return (null, null);

            return (Serialize(changedOld), Serialize(changedNew));
        }

        private static string? Serialize(IDictionary<string, object?> values)
        {
            try
            {
                return JsonConvert.SerializeObject(values, Formatting.None);
            }
            catch
            {
                return null;
            }
        }

        private static bool AreEqual(object? a, object? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            if (a.Equals(b)) return true;

            // Same logical number stored as different CLR types (int vs long vs decimal).
            if (IsNumeric(a) && IsNumeric(b))
            {
                try { return Convert.ToDecimal(a, CultureInfo.InvariantCulture) == Convert.ToDecimal(b, CultureInfo.InvariantCulture); }
                catch { return false; }
            }

            if (a is DateTime da && b is DateTime db2) return da == db2;

            return string.Equals(
                Convert.ToString(a, CultureInfo.InvariantCulture),
                Convert.ToString(b, CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        }

        // =============================================================
        // HELPERS
        // =============================================================

        private static object? Normalize(object? value)
        {
            switch (value)
            {
                case null:
                    return null;
                case string s:
                    return Truncate(s, MaxStringLength);
                case DateTime dt:
                    return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                case bool b:
                    return b ? "true" : "false";
                case decimal d:
                    return d.ToString(CultureInfo.InvariantCulture);
                case byte[] bytes:
                    return $"[{bytes.Length} bytes]";
                default:
                    if (value.GetType().IsEnum) return value.ToString();
                    return Truncate(Convert.ToString(value, CultureInfo.InvariantCulture), MaxStringLength);
            }
        }

        private static bool IsScalar(Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;

            if (t.IsEnum) return true;

            return t == typeof(string)
                || t == typeof(bool)
                || t == typeof(byte)
                || t == typeof(sbyte)
                || t == typeof(short)
                || t == typeof(ushort)
                || t == typeof(int)
                || t == typeof(uint)
                || t == typeof(long)
                || t == typeof(ulong)
                || t == typeof(float)
                || t == typeof(double)
                || t == typeof(decimal)
                || t == typeof(DateTime)
                || t == typeof(DateTimeOffset)
                || t == typeof(TimeSpan)
                || t == typeof(Guid);
        }

        private static bool IsNumeric(object value)
        {
            return value is byte or sbyte or short or ushort or int or uint
                or long or ulong or float or double or decimal;
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }

        private static string? ResolveIpAddress(HttpContext? httpContext)
        {
            if (httpContext == null) return null;

            var forwarded = httpContext.Request?.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded)) return Truncate(forwarded.Split(',')[0].Trim(), 100);

            return Truncate(httpContext.Connection?.RemoteIpAddress?.ToString(), 100);
        }

        public async Task<byte[]> ExportExcel(IList<QueryFilters> filters)
        {
            var rows = await GetExportDataAsync(filters);

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Audit Logs");

                var navyColor = XLColor.FromHtml("#1F4E78");

                ws.Cell("A1").Value = "AUDIT LOG REPORT";
                ws.Cell("A1").Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(navyColor);

                string[] headers = {
                    "Audit Date & Time", "User", "User Type", "Module", "Action",
                    "Reference/Entity ID", "Description", "Old Values", "New Values",
                    "IP Address", "Page Name", "Entity Name"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(3, i + 1).Value = headers[i];
                    ws.Cell(3, i + 1).Style.Font.SetBold();
                    ws.Cell(3, i + 1).Style.Fill.BackgroundColor = navyColor;
                    ws.Cell(3, i + 1).Style.Font.FontColor = XLColor.White;
                    ws.Cell(3, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Cell(3, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                int r = 4;
                foreach (var item in rows)
                {
                    ws.Cell(r, 1).Value = item.AuditDate;
                    ws.Cell(r, 2).Value = item.UserName ?? "System";
                    ws.Cell(r, 3).Value = item.UserType;
                    ws.Cell(r, 4).Value = item.Module;
                    ws.Cell(r, 5).Value = item.Action;
                    ws.Cell(r, 6).Value = item.EntityId;
                    ws.Cell(r, 7).Value = item.Description;
                    ws.Cell(r, 8).Value = item.OldValues;
                    ws.Cell(r, 9).Value = item.NewValues;
                    ws.Cell(r, 10).Value = item.IpAddress;
                    ws.Cell(r, 11).Value = item.PageName;
                    ws.Cell(r, 12).Value = item.EntityName;

                    ws.Row(r).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                    ws.Row(r).Style.Alignment.WrapText = true;

                    for (int c = 1; c <= headers.Length; c++)
                        ws.Cell(r, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                    r++;
                }

                if (rows.Count == 0)
                {
                    ws.Cell(r, 1).Value = "No records found";
                    ws.Range(r, 1, r, headers.Length).Merge();
                    ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    for (int c = 1; c <= headers.Length; c++)
                        ws.Cell(r, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    r++;
                }

                ws.Columns().AdjustToContents();
                ws.Column(7).Width = 50;
                ws.Column(8).Width = 50;
                ws.Column(9).Width = 50;

                using (var ms = new System.IO.MemoryStream())
                {
                    workbook.SaveAs(ms);
                    return ms.ToArray();
                }
            }
        }

    public async Task<byte[]> ExportPdf(IList<QueryFilters> filters)
    {
        var rows = await GetExportDataAsync(filters);

        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:'Segoe UI',Arial,sans-serif;font-size:7px;color:#212529;}");
        sb.AppendLine("h2{color:#1F4E78;margin:0 0 4px 0;font-size:14px;}");
        sb.AppendLine(".filter-line{font-size:8px;color:#595959;margin-bottom:10px;}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;}");
        sb.AppendLine("th,td{border:1px solid #999;padding:3px 4px;text-align:left;word-break:break-word;}");
        sb.AppendLine("th{background:#1F4E78;color:#fff;font-weight:bold;white-space:nowrap;}");
        sb.AppendLine("tr:nth-child(even){background:#F9FAFB;}");
        sb.AppendLine(".small{font-size:6px;}");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine("<h2>Audit Log Report</h2>");
        sb.AppendLine("<div class='filter-line'>Generated on: " + DateTime.Now.ToString("dd-MMM-yyyy HH:mm") + "</div>");

        sb.AppendLine("<table>");
        sb.AppendLine("<thead><tr>");
        sb.AppendLine("<th>Date & Time</th><th>User</th><th>User Type</th><th>Module</th><th>Action</th><th>Ref/Entity ID</th><th>Description</th><th>Old Values</th><th>New Values</th><th>IP</th><th>Page Name</th><th>Entity Name</th>");
        sb.AppendLine("</tr></thead>");
        sb.AppendLine("<tbody>");

        foreach (var item in rows)
        {
            string auditDate = item.AuditDate.HasValue ? item.AuditDate.Value.ToString("dd-MMM-yyyy HH:mm:ss") : string.Empty;
            string user = string.IsNullOrWhiteSpace(item.UserName) ? "System" : item.UserName;
            string oldVal = !string.IsNullOrWhiteSpace(item.OldValues) ? item.OldValues : string.Empty;
            string newVal = !string.IsNullOrWhiteSpace(item.NewValues) ? item.NewValues : string.Empty;
            string desc = !string.IsNullOrWhiteSpace(item.Description) ? item.Description : string.Empty;
            string userType = item.UserType.HasValue ? item.UserType.Value.ToString() : string.Empty;

            sb.AppendLine("<tr>");
            sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(auditDate)}</td>");
            sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(user)}</td>");
            sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(userType)}</td>");
            sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.Module ?? string.Empty)}</td>");
            sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.Action ?? string.Empty)}</td>");
            sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.EntityId ?? string.Empty)}</td>");
            sb.AppendLine($"<td class='small'>{System.Net.WebUtility.HtmlEncode(desc)}</td>");
            sb.AppendLine($"<td class='small'>{System.Net.WebUtility.HtmlEncode(oldVal)}</td>");
            sb.AppendLine($"<td class='small'>{System.Net.WebUtility.HtmlEncode(newVal)}</td>");
            sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.IpAddress ?? string.Empty)}</td>");
            sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.PageName ?? string.Empty)}</td>");
            sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.EntityName ?? string.Empty)}</td>");
            sb.AppendLine("</tr>");
        }

        if (rows.Count == 0)
        {
            sb.AppendLine("<tr><td colspan='12' style='text-align:center'>No records found</td></tr>");
        }

            sb.AppendLine("</tbody></table>");
            sb.AppendLine("</body></html>");

            var doc = new HtmlToPdfDocument
            {
                GlobalSettings = new GlobalSettings
                {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Landscape,
                    PaperSize = PaperKind.A4,
                    Margins = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10 },
                    DocumentTitle = "Audit Log Report"
                },
                Objects =
                {
                    new ObjectSettings
                    {
                        PagesCount = true,
                        HtmlContent = sb.ToString(),
                        WebSettings = { DefaultEncoding = "utf-8", LoadImages = true }
                    }
                }
            };

            try
            {
                return await Task.FromResult(_converter.Convert(doc));
            }
            catch (AggregateException agg) when (agg.InnerException != null)
            {
                throw new InvalidOperationException(
                    "PDF export failed: the wkhtmltopdf engine (libwkhtmltox.dll) could not be loaded. " +
                    "Ensure libwkhtmltox.dll is present next to the application executable. " +
                    "Cause: " + agg.InnerException.Message,
                    agg.InnerException);
            }
            catch (DllNotFoundException dllEx)
            {
                throw new InvalidOperationException(
                    "PDF export failed: libwkhtmltox.dll is missing next to the application executable.",
                    dllEx);
            }
        }

        private async Task<List<AuditLogExport>> GetExportDataAsync(IList<QueryFilters> filters)
        {
            var query = _context.AuditLogs.AsNoTracking();

            if (filters != null)
            {
                foreach (var f in filters)
                {
                    if (string.IsNullOrWhiteSpace(f.fieldName) || string.IsNullOrWhiteSpace(f.filterValue)) continue;

                    string val = f.filterValue.Trim();
                    if (val == string.Empty) continue;

                    switch (f.fieldName.ToLowerInvariant())
                    {   
                        case "module":
                        {
                            var up = val.ToUpper();
                            query = query.Where(x => x.Module != null && x.Module.ToUpper() == up);
                            break;
                        }
                        case "action":
                        {
                            var up = val.ToUpper();
                            query = query.Where(x => x.Action != null && x.Action.ToUpper() == up);
                            break;
                        }
                        case "username":
                        case "user":
                            query = query.Where(x => x.UserName != null && x.UserName.Contains(val, StringComparison.OrdinalIgnoreCase));
                            break;
                        case "entityid":
                        case "reference":
                            query = query.Where(x => x.EntityId != null && x.EntityId.Contains(val, StringComparison.OrdinalIgnoreCase));
                            break;
                        case "fromdate":
                            if (DateTime.TryParse(val, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var from) ||
                                DateTime.TryParse(val, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out from) ||
                                DateTime.TryParseExact(val, new[] { "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy", "dd/MM/yyyy" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out from))
                            {
                                query = query.Where(x => x.AuditDate >= from);
                            }
                            break;
                        case "todate":
                            if (DateTime.TryParse(val, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var to) ||
                                DateTime.TryParse(val, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out to) ||
                                DateTime.TryParseExact(val, new[] { "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy", "dd/MM/yyyy" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out to))
                            {
                                query = query.Where(x => x.AuditDate < to.AddDays(1));
                            }
                            break;
                        case "search":
                        case "global":
                        case "search[value]":
                            query = query.Where(x =>
                                (x.Description != null && EF.Functions.Like(x.Description, "%" + val + "%")) ||
                                (x.EntityId != null && EF.Functions.Like(x.EntityId, "%" + val + "%")) ||
                                (x.UserName != null && EF.Functions.Like(x.UserName, "%" + val + "%")) ||
                                (x.Module != null && EF.Functions.Like(x.Module, "%" + val + "%")) ||
                                (x.Action != null && EF.Functions.Like(x.Action, "%" + val + "%")) ||
                                (x.IpAddress != null && EF.Functions.Like(x.IpAddress, "%" + val + "%")) ||
                                (x.PageName != null && EF.Functions.Like(x.PageName, "%" + val + "%")) ||
                                (x.EntityName != null && EF.Functions.Like(x.EntityName, "%" + val + "%")));
                            break;
                    }
                }
            }

            var rows = await query
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            var nameMap = await BuildNameMapAsync(rows);

            var result = new List<AuditLogExport>(rows.Count);
            foreach (var x in rows)
            {
                result.Add(new AuditLogExport
                {
                    AuditDate = x.AuditDate,
                    UserName = x.UserName,
                    UserType = x.UserType,
                    Module = x.Module,
                    Action = x.Action,
                    EntityName = x.EntityName,
                    EntityId = x.EntityId,
                    Description = ResolveDescription(x.Description, nameMap),
                    OldValues = x.OldValues,
                    NewValues = x.NewValues,
                    IpAddress = x.IpAddress,
                    UserAgent = x.UserAgent,
                    PageName = x.PageName
                });
            }

            return result;
        }

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
                var customers = await _context.Customers
                    .AsNoTracking()
                    .Where(c => customerIds.Contains(c.Id))
                    .Select(c => new { c.Id, c.CustomerName, c.CustomerCode })
                    .ToListAsync();

                foreach (var c in customers)
                {
                    map.AddCustomer(c.Id, c.CustomerName ?? c.CustomerCode);
                }

                var jobs = await _context.JobImportMasters
                    .AsNoTracking()
                    .Where(j => jobIds.Contains(j.Id))
                    .Select(j => new { j.Id, j.JobNumber })
                    .ToListAsync();

                foreach (var j in jobs)
                {
                    map.AddJob(j.Id, j.JobNumber);
                }
            }
            catch { }

            return map;
        }

        private static void CollectIds(string? json, HashSet<int> customerIds, HashSet<int> jobIds)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            JObject payload;
            try { payload = JObject.Parse(json); }
            catch { return; }

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

        private static string ResolveDescription(string? description, AuditNameMap map)
        {
            if (string.IsNullOrWhiteSpace(description)) return description ?? string.Empty;

            string Replace(string input, string pattern, string keyPrefix)
            {
                return System.Text.RegularExpressions.Regex.Replace(
                    input,
                    pattern,
                    m => map.Lookup.TryGetValue($"{keyPrefix}{m.Groups[1].Value}", out var name) ? name : m.Value);
            }

            description = Replace(description, @"\bcustomer: #(\d+)", "CustomerId:");
            description = Replace(description, @"\bcustomer #(\d+)", "CustomerId:");
            description = Replace(description, @"\bservice provider #(\d+)", "CustomerId:");
            description = Replace(description, @"\bjob #(\d+)", "JobId:");

            description = System.Text.RegularExpressions.Regex.Replace(
                description,
                @"\bstatus (\d+) -> (\d+)\b",
                m => $"status {BusinessLogic.Helper.StatusHelper.GetInvoiceStatus(int.Parse(m.Groups[1].Value))}" +
                     $" -> {BusinessLogic.Helper.StatusHelper.GetInvoiceStatus(int.Parse(m.Groups[2].Value))}");

            return description;
        }

        private class AuditLogExport
        {
            public DateTime? AuditDate { get; set; }
            public string? UserName { get; set; }
            public int? UserType { get; set; }
            public string? Module { get; set; }
            public string? Action { get; set; }
            public string? EntityName { get; set; }
            public string? EntityId { get; set; }
            public string? Description { get; set; }
            public string? OldValues { get; set; }
            public string? NewValues { get; set; }
            public string? IpAddress { get; set; }
            public string? UserAgent { get; set; }
            public string? PageName { get; set; }
        }
    }
}
