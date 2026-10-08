using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BusinessLogic.Models;
using BusinessLogic.Interfaces;

namespace JobApplication.Controllers
{
    public class SettingController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ISessionHelper _session;
        private readonly IAuditService _audit;

        public SettingController(AppDbContext db, ISessionHelper session, IAuditService audit)
        {
            _db = db;
            _session = session;
            _audit = audit;
        }

        // Generic settings index. ?type=jobtype|bltype|containertype|country
        [HttpGet("/Setting")]
        [HttpGet("/Setting/Index")]
        public IActionResult Index(string type = "jobtype")
        {
            ViewBag.SettingType = type?.ToLower() ?? "jobtype";
            return View();
        }

        [HttpPost]
        public IActionResult GetItems(string type)
        {
            type = (type ?? "").ToLower();

            // If request contains datatables parameters, return server-side paged response
            if (Request.HasFormContentType && Request.Form.ContainsKey("draw"))
            {
                int draw = Convert.ToInt32(Request.Form["draw"].FirstOrDefault() ?? "0");
                int start = Convert.ToInt32(Request.Form["start"].FirstOrDefault() ?? "0");
                int length = Convert.ToInt32(Request.Form["length"].FirstOrDefault() ?? "10");
                string search = (Request.Form["search[value]"].FirstOrDefault() ?? "").Trim();
                int orderCol = Convert.ToInt32(Request.Form["order[0][column]"].FirstOrDefault() ?? "1");
                bool desc = (Request.Form["order[0][dir]"].FirstOrDefault() ?? "asc")
                    .Equals("desc", StringComparison.OrdinalIgnoreCase);

                // handle each type explicitly to avoid dynamic expression trees
                if (type == "bltype")
                {
                    var q = _db.Bltypes.AsQueryable();
                    int recordsTotal = q.Count();
                    if (!string.IsNullOrEmpty(search)) q = q.Where(x => x.Name.Contains(search));
                    int recordsFiltered = q.Count();
                    if (orderCol == 2)
                        q = desc ? q.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.Name) : q.OrderBy(x => x.IsActive).ThenBy(x => x.Name);
                    else
                        q = desc ? q.OrderByDescending(x => x.Name) : q.OrderBy(x => x.Name);
                    var data = q.Skip(start).Take(length).Select(x => new { x.Id, x.Name, x.IsActive }).ToList();
                    return Json(new { draw = draw, recordsTotal = recordsTotal, recordsFiltered = recordsFiltered, data = data });
                }

                if (type == "containertype")
                {
                    var q = _db.ContainerTypes.AsQueryable();
                    int recordsTotal = q.Count();
                    if (!string.IsNullOrEmpty(search)) q = q.Where(x => x.Name.Contains(search));
                    int recordsFiltered = q.Count();
                    if (orderCol == 2)
                        q = desc ? q.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.Name) : q.OrderBy(x => x.IsActive).ThenBy(x => x.Name);
                    else
                        q = desc ? q.OrderByDescending(x => x.Name) : q.OrderBy(x => x.Name);
                    var data = q.Skip(start).Take(length).Select(x => new { x.Id, x.Name, x.IsActive }).ToList();
                    return Json(new { draw = draw, recordsTotal = recordsTotal, recordsFiltered = recordsFiltered, data = data });
                }

                if (type == "country")
                {
                    var q = _db.Countries.AsQueryable();
                    int recordsTotal = q.Count();
                    if (!string.IsNullOrEmpty(search)) q = q.Where(x => x.Name.Contains(search));
                    int recordsFiltered = q.Count();
                    if (orderCol == 2)
                        q = desc ? q.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.Name) : q.OrderBy(x => x.IsActive).ThenBy(x => x.Name);
                    else
                        q = desc ? q.OrderByDescending(x => x.Name) : q.OrderBy(x => x.Name);
                    var data = q.Skip(start).Take(length).Select(x => new { x.Id, x.Name, x.IsActive }).ToList();
                    return Json(new { draw = draw, recordsTotal = recordsTotal, recordsFiltered = recordsFiltered, data = data });
                }

                // paymentheader
                if (type == "paymentheader")
                {
                    var q = _db.PaymentHeaders.AsQueryable();
                    int recordsTotal = q.Count();
                    if (!string.IsNullOrEmpty(search)) q = q.Where(x => (x.Headers != null && x.Headers.Contains(search)) || (x.HeadersAr != null && x.HeadersAr.Contains(search)));
                    int recordsFiltered = q.Count();
                    switch (orderCol)
                    {
                        case 0: q = desc ? q.OrderByDescending(x => x.Id) : q.OrderBy(x => x.Id); break;
                        case 2: q = desc ? q.OrderByDescending(x => x.HeadersAr) : q.OrderBy(x => x.HeadersAr); break;
                        case 3: q = desc ? q.OrderByDescending(x => x.PaymentTypeId) : q.OrderBy(x => x.PaymentTypeId); break;
                        case 4: q = desc ? q.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.Headers) : q.OrderBy(x => x.IsActive).ThenBy(x => x.Headers); break;
                        default: q = desc ? q.OrderByDescending(x => x.Headers) : q.OrderBy(x => x.Headers); break;
                    }
                    var data = q.Skip(start).Take(length).Select(x => new { x.Id, x.Headers, x.HeadersAr, x.PaymentTypeId, x.IsActive }).ToList();
                    return Json(new { draw = draw, recordsTotal = recordsTotal, recordsFiltered = recordsFiltered, data = data });
                }

                // default jobtype
                {
                    var q = _db.JobTypes.AsQueryable();
                    int recordsTotal = q.Count();
                    if (!string.IsNullOrEmpty(search)) q = q.Where(x => x.Name.Contains(search));
                    int recordsFiltered = q.Count();
                    if (orderCol == 2)
                        q = desc ? q.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.Name) : q.OrderBy(x => x.IsActive).ThenBy(x => x.Name);
                    else
                        q = desc ? q.OrderByDescending(x => x.Name) : q.OrderBy(x => x.Name);
                    var data = q.Skip(start).Take(length).Select(x => new { x.Id, x.Name, x.IsActive }).ToList();
                    return Json(new { draw = draw, recordsTotal = recordsTotal, recordsFiltered = recordsFiltered, data = data });
                }
            }

            // Fallback: return all
            var result = type switch
            {
                "bltype" => (object)_db.Bltypes.Select(x => new { x.Id, x.Name, x.IsActive }).ToList(),
                "containertype" => (object)_db.ContainerTypes.Select(x => new { x.Id, x.Name, x.IsActive }).ToList(),
                "country" => (object)_db.Countries.Select(x => new { x.Id, x.Name, x.IsActive }).ToList(),
                "paymentheader" => (object)_db.PaymentHeaders.Select(x => new { x.Id, x.Headers, x.PaymentTypeId, x.Vat, x.IsActive }).ToList(),
                _ => (object)_db.JobTypes.Select(x => new { x.Id, x.Name, x.IsActive }).ToList(),
            };

            return Json(new { success = true, data = result });
        }

        [HttpPost]
        public async Task<IActionResult> SaveItem(string type, int id, string name, string headers, string headersAr, decimal? vat, int? paymentTypeId)
        {
            type = (type ?? "").ToLower();
            // For paymentheader we don't require the generic 'name' field
            if (type != "paymentheader")
            {
                if (string.IsNullOrWhiteSpace(name))
                    return Json(new { success = false, message = "Name required" });

                name = name.Trim();
            }

            // ========== AUDIT: snapshot the current state before mutating ==========
            // Snapshot() copies the values out, so later mutation of the tracked
            // entity cannot leak into the "before" side of the diff.
            var auditBefore = _audit.Snapshot(await LoadSettingEntityAsync(type, id));

            switch (type)
            {
                case "bltype":
                    if (id == 0) _db.Bltypes.Add(new Bltype { Name = name, IsActive = true });
                    else
                    {
                        var e = await _db.Bltypes.FindAsync(id);
                        if (e == null) return Json(new { success = false, message = "Not found" });
                        e.Name = name;
                    }
                    break;
                case "containertype":
                    if (id == 0) _db.ContainerTypes.Add(new ContainerType { Name = name, IsActive = true });
                    else
                    {
                        var e = await _db.ContainerTypes.FindAsync(id);
                        if (e == null) return Json(new { success = false, message = "Not found" });
                        e.Name = name;
                    }
                    break;
                case "country":
                    if (id == 0) _db.Countries.Add(new Country { Name = name, IsActive = true });
                    else
                    {
                        var e = await _db.Countries.FindAsync(id);
                        if (e == null) return Json(new { success = false, message = "Not found" });
                        e.Name = name;
                    }
                    break;
                case "paymentheader":
                    // headers -> Headers field, headersAr -> HeadersAr, vat -> Vat, paymentTypeId -> PaymentTypeId
                    if (string.IsNullOrWhiteSpace(headers))
                        return Json(new { success = false, message = "Headers required" });

                    if (id == 0)
                    {
                        _db.PaymentHeaders.Add(new PaymentHeader
                        {
                            Headers = headers.Trim(),
                            HeadersAr = headersAr?.Trim(),
                            Vat = vat,
                            PaymentTypeId = paymentTypeId,
                            IsActive = true,
                            CreatedOn = DateTime.Now
                        });
                    }
                    else
                    {
                        var e = await _db.PaymentHeaders.FindAsync(id);
                        if (e == null) return Json(new { success = false, message = "Not found" });
                        e.Headers = headers.Trim();
                        e.HeadersAr = headersAr?.Trim();
                        e.Vat = vat;
                        e.PaymentTypeId = paymentTypeId;
                    }
                    break;
                default:
                    if (id == 0) _db.JobTypes.Add(new JobType { Name = name, IsActive = true });
                    else
                    {
                        var e = await _db.JobTypes.FindAsync(id);
                        if (e == null) return Json(new { success = false, message = "Not found" });
                        e.Name = name;
                    }
                    break;
            }

            await _db.SaveChangesAsync();

            // ========== AUDIT: record lookup-item create / update ==========
            var createdId = id;
            if (createdId == 0)
            {
                // Re-read to pick up the identity assigned by the insert.
                createdId = await NewestSettingIdAsync(type);
            }

            var auditAfter = _audit.Snapshot(await LoadSettingEntityAsync(type, createdId));

            await RecordSettingAsync(
                type, createdId, id == 0 ? "Create" : "Update",
                auditBefore, auditAfter,
                $"{SettingTitle(type)} '{SettingLabel(type, await LoadSettingEntityAsync(type, createdId))}' " +
                (id == 0 ? "created." : "updated."));

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> GetById(string type, int id)
        {
            type = (type ?? "").ToLower();
            object? entity = type switch
            {
                "bltype" => await _db.Bltypes.FindAsync(id),
                "containertype" => await _db.ContainerTypes.FindAsync(id),
                "country" => await _db.Countries.FindAsync(id),
                "paymentheader" => await _db.PaymentHeaders.FindAsync(id),
                _ => await _db.JobTypes.FindAsync(id),
            };

            if (entity == null) return Json(new { success = false });
            return Json(new { success = true, data = entity });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(string type, int id)
        {
            type = (type ?? "").ToLower();

            // ========== AUDIT: snapshot before the flag flips ==========
            var auditBefore = _audit.Snapshot(await LoadSettingEntityAsync(type, id));

            switch (type)
            {
                case "bltype":
                    var b = await _db.Bltypes.FindAsync(id);
                    if (b == null) return Json(new { success = false });
                    b.IsActive = !(b.IsActive ?? false);
                    break;
                case "containertype":
                    var c = await _db.ContainerTypes.FindAsync(id);
                    if (c == null) return Json(new { success = false });
                    c.IsActive = !(c.IsActive ?? false);
                    break;
                case "paymentheader":
                    var ph = await _db.PaymentHeaders.FindAsync(id);
                    if (ph == null) return Json(new { success = false });
                    ph.IsActive = !(ph.IsActive ?? false);
                    break;
                case "country":
                    var co = await _db.Countries.FindAsync(id);
                    if (co == null) return Json(new { success = false });
                    co.IsActive = !(co.IsActive ?? false);
                    break;
                default:
                    var j = await _db.JobTypes.FindAsync(id);
                    if (j == null) return Json(new { success = false });
                    j.IsActive = !(j.IsActive ?? false);
                    break;
            }

            await _db.SaveChangesAsync();

            // ========== AUDIT: record status change ==========
            var auditEntity = await LoadSettingEntityAsync(type, id);
            var auditAfter = _audit.Snapshot(auditEntity);
            var nowActive = auditEntity switch
            {
                Bltype x => x.IsActive ?? false,
                ContainerType x => x.IsActive ?? false,
                PaymentHeader x => x.IsActive ?? false,
                Country x => x.IsActive ?? false,
                JobType x => x.IsActive ?? false,
                _ => (bool?)null
            };

            await RecordSettingAsync(
                type, id, "Toggle Status", auditBefore, auditAfter,
                $"{SettingTitle(type)} '{SettingLabel(type, auditEntity)}' " +
                $"{(nowActive == true ? "enabled" : "disabled")} by user #{_session.LoginId}.");

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteItem(string type, int id)
        {
            type = (type ?? "").ToLower();

            // ========== AUDIT: keep the row that is about to disappear ==========
            var auditBefore = _audit.Snapshot(await LoadSettingEntityAsync(type, id));

            switch (type)
            {
                case "bltype":
                    var b = await _db.Bltypes.FindAsync(id);
                    if (b != null) _db.Bltypes.Remove(b);
                    break;
                case "containertype":
                    var c = await _db.ContainerTypes.FindAsync(id);
                    if (c != null) _db.ContainerTypes.Remove(c);
                    break;
                case "country":
                    var co = await _db.Countries.FindAsync(id);
                    if (co != null) _db.Countries.Remove(co);
                    break;
                default:
                    var j = await _db.JobTypes.FindAsync(id);
                    if (j != null) _db.JobTypes.Remove(j);
                    break;
            }

            await _db.SaveChangesAsync();

            // ========== AUDIT: record delete ==========
            if (auditBefore != null)
            {
                await RecordSettingAsync(
                    type, id, "Delete", auditBefore, null,
                    $"{SettingTitle(type)} '{SettingLabel(type, null, auditBefore)}' deleted by user #{_session.LoginId}.");
            }

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> GetVatSetting()
        {
            var ph = await _db.PaymentHeaders.FirstOrDefaultAsync(x => x.PaymentTypeId == 2);
            return Json(new { success = true, vat = ph?.Vat });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateVatSetting(decimal? vat)
        {
            var list = await _db.PaymentHeaders.Where(x => x.PaymentTypeId == 2).ToListAsync();

            // ========== AUDIT: snapshot before applying the new rate ==========
            var auditBefore = _audit.Snapshot(list.FirstOrDefault());

            if (list.Count == 0)
            {
                _db.PaymentHeaders.Add(new PaymentHeader
                {
                    PaymentTypeId = 2,
                    Vat = vat,
                    IsActive = true,
                    CreatedOn = DateTime.Now,
                    CreatedBy = _session.LoginId
                });
            }
            else
            {
                foreach (var ph in list)
                {
                    ph.Vat = vat;
                    ph.ModifiedBy = _session.LoginId;
                    ph.ModifiedOn = DateTime.Now;
                }
            }
            await _db.SaveChangesAsync();

            // ========== AUDIT: record VAT rate change ==========
            var auditAfter = _audit.Snapshot(await _db.PaymentHeaders
                .Where(x => x.PaymentTypeId == 2).FirstOrDefaultAsync());

            await _audit.RecordAsync(new AuditEntryRequest
            {
                Module = "Settings",
                Action = "Update",
                EntityName = "PaymentHeader",
                EntityId = "VAT Rate",
                Description = $"Default VAT rate changed by user #{_session.LoginId}.",
                OldValues = auditBefore,
                NewValues = auditAfter,
                PageName = "/Setting?type=general"
            });

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> GetQrCodeSetting()
        {
            var setting = await _db.Settings.FirstOrDefaultAsync(x => x.SettingKey == "EnableInvoiceQRCode");
            bool enabled = setting?.IsActive ?? false;
            return Json(new { success = true, enabled });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateQrCodeSetting(bool enabled)
        {
            if (_session.UserType != 1)
            {
                return Json(new { success = false, message = "Super Admin only." });
            }

            var setting = await _db.Settings.FirstOrDefaultAsync(x => x.SettingKey == "EnableInvoiceQRCode");
            bool auditWasEnabled = setting?.IsActive ?? false;
            bool isEdit = setting != null;

            if (setting == null)
            {
                _db.Settings.Add(new Setting
                {
                    SettingKey = "EnableInvoiceQRCode",
                    SettingValue = enabled.ToString(),
                    IsActive = enabled,
                    CreatedBy = _session.LoginId,
                    CreatedOn = DateTime.Now
                });
            }
            else
            {
                setting.SettingValue = enabled.ToString();
                setting.IsActive = enabled;
                setting.ModifiedBy = _session.LoginId;
                setting.ModifiedOn = DateTime.Now;
            }
            await _db.SaveChangesAsync();

            await _audit.RecordAsync(new AuditEntryRequest
            {
                Module = "Settings",
                Action = "Update",
                EntityName = "Setting",
                EntityId = "EnableInvoiceQRCode",
                Description = $"Invoice QR Code (ZATCA) {(enabled ? "enabled" : "disabled")} by user #{_session.LoginId}.",
                OldValues = new Dictionary<string, object?> { { "IsActive", auditWasEnabled ? "true" : "false" } },
                NewValues = new Dictionary<string, object?> { { "IsActive", enabled ? "true" : "false" } },
                PageName = "/Setting?type=general"
            });

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> GetTwoFactorSetting()
        {
            var setting = await _db.Settings.FirstOrDefaultAsync(x => x.SettingKey == "EnableTwoFactorAuth");
            bool enabled = setting?.IsActive ?? false;
            return Json(new { success = true, enabled });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateTwoFactorSetting(bool enabled)
        {
            if (_session.UserType != 1)
            {
                return Json(new { success = false, message = "Super Admin only." });
            }

            var setting = await _db.Settings.FirstOrDefaultAsync(x => x.SettingKey == "EnableTwoFactorAuth");
            bool auditWasEnabled = setting?.IsActive ?? false;

            if (setting == null)
            {
                _db.Settings.Add(new Setting
                {
                    SettingKey = "EnableTwoFactorAuth",
                    SettingValue = enabled.ToString(),
                    IsActive = enabled,
                    CreatedBy = _session.LoginId,
                    CreatedOn = DateTime.Now
                });
            }
            else
            {
                setting.SettingValue = enabled.ToString();
                setting.IsActive = enabled;
                setting.ModifiedBy = _session.LoginId;
                setting.ModifiedOn = DateTime.Now;
            }
            await _db.SaveChangesAsync();

            await _audit.RecordAsync(new AuditEntryRequest
            {
                Module = "Settings",
                Action = "Update",
                EntityName = "Setting",
                EntityId = "EnableTwoFactorAuth",
                Description = $"Two-factor authentication {(enabled ? "ENABLED" : "DISABLED")} for all users by user #{_session.LoginId}. Previous state: {(auditWasEnabled ? "enabled" : "disabled")}.",
                OldValues = new Dictionary<string, object?> { { "IsActive", auditWasEnabled ? "true" : "false" } },
                NewValues = new Dictionary<string, object?> { { "IsActive", enabled ? "true" : "false" } },
                PageName = "/Setting?type=general"
            });

            return Json(new { success = true });
        }

        // =============================================================
        // AUDIT HELPERS - lookup / master-data setting items
        // =============================================================

        /// <summary>
        /// Loads the right lookup entity for the given type. Kept separate from the
        /// switch statements above so audit can read state without duplicating the
        /// "find or fail" branches.
        /// </summary>
        private async Task<object?> LoadSettingEntityAsync(string type, int id)
        {
            if (id <= 0) return null;

            return type switch
            {
                "bltype" => await _db.Bltypes.FindAsync(id),
                "containertype" => await _db.ContainerTypes.FindAsync(id),
                "country" => await _db.Countries.FindAsync(id),
                "paymentheader" => await _db.PaymentHeaders.FindAsync(id),
                _ => await _db.JobTypes.FindAsync(id)
            };
        }

        /// <summary>
        /// After an insert the client still holds id == 0, so read back the highest
        /// identity to give the audit entry a meaningful reference.
        /// </summary>
        private async Task<int> NewestSettingIdAsync(string type)
        {
            int? newest = type switch
            {
                "bltype" => await _db.Bltypes.Select(x => (int?)x.Id).MaxAsync(),
                "containertype" => await _db.ContainerTypes.Select(x => (int?)x.Id).MaxAsync(),
                "country" => await _db.Countries.Select(x => (int?)x.Id).MaxAsync(),
                "paymentheader" => await _db.PaymentHeaders.Select(x => (int?)x.Id).MaxAsync(),
                _ => await _db.JobTypes.Select(x => (int?)x.Id).MaxAsync()
            };

            return newest ?? 0;
        }

        /// <summary>Human label for the entity, e.g. "BL Type".</summary>
        private static string SettingTitle(string type)
        {
            return type switch
            {
                "bltype" => "BL Type",
                "containertype" => "Container Type",
                "country" => "Country",
                "paymentheader" => "Payment Header",
                _ => "Job Type"
            };
        }

        /// <summary>Table name, used as the audit EntityName.</summary>
        private static string SettingEntityName(string type)
        {
            return type switch
            {
                "bltype" => nameof(Bltype),
                "containertype" => nameof(ContainerType),
                "country" => nameof(Country),
                "paymentheader" => nameof(PaymentHeader),
                _ => nameof(JobType)
            };
        }

        /// <summary>
        /// The record's display value. Falls back to the snapshot dictionary when the
        /// entity is already gone (delete case).
        /// </summary>
        private static string SettingLabel(string type, object? entity, IDictionary<string, object?>? fallback = null)
        {
            string? val = entity switch
            {
                Bltype x => x.Name,
                ContainerType x => x.Name,
                Country x => x.Name,
                PaymentHeader x => x.Headers,
                JobType x => x.Name,
                _ => null
            };

            val ??= fallback?["Name"]?.ToString() ?? fallback?["Headers"]?.ToString();

            return string.IsNullOrWhiteSpace(val) ? "(no name)" : val!;
        }

        private Task RecordSettingAsync(
            string type,
            int id,
            string action,
            IDictionary<string, object?>? before,
            IDictionary<string, object?>? after,
            string description)
        {
            return _audit.RecordAsync(new AuditEntryRequest
            {
                Module = "Settings",
                Action = action,
                EntityName = SettingEntityName(type),
                EntityId = $"{SettingTitle(type)} #{id}",
                Description = description,
                OldValues = before,
                NewValues = after,
                PageName = $"/Setting?type={type}"
            });
        }
    }
}
