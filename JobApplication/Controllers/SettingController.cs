using Microsoft.AspNetCore.Mvc;
using BusinessLogic.Models;

namespace JobApplication.Controllers
{
    public class SettingController : Controller
    {
        private readonly AppDbContext _db;

        public SettingController(AppDbContext db)
        {
            _db = db;
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
                string search = (Request.Form["search[value]"] .FirstOrDefault() ?? "").Trim();

                IQueryable<dynamic> query = type switch
                {
                    "bltype" => _db.Bltypes.AsQueryable(),
                    "containertype" => _db.ContainerTypes.AsQueryable(),
                    "country" => _db.Countries.AsQueryable(),
                    _ => _db.JobTypes.AsQueryable(),
                };

                // handle each type explicitly to avoid dynamic expression trees
                if (type == "bltype")
                {
                    var q = _db.Bltypes.AsQueryable();
                    int recordsTotal = q.Count();
                    if (!string.IsNullOrEmpty(search)) q = q.Where(x => x.Name.Contains(search));
                    int recordsFiltered = q.Count();
                    var data = q.OrderBy(x => x.Name).Skip(start).Take(length).Select(x => new { x.Id, x.Name, x.IsActive }).ToList();
                    return Json(new { draw = draw, recordsTotal = recordsTotal, recordsFiltered = recordsFiltered, data = data });
                }

                if (type == "containertype")
                {
                    var q = _db.ContainerTypes.AsQueryable();
                    int recordsTotal = q.Count();
                    if (!string.IsNullOrEmpty(search)) q = q.Where(x => x.Name.Contains(search));
                    int recordsFiltered = q.Count();
                    var data = q.OrderBy(x => x.Name).Skip(start).Take(length).Select(x => new { x.Id, x.Name, x.IsActive }).ToList();
                    return Json(new { draw = draw, recordsTotal = recordsTotal, recordsFiltered = recordsFiltered, data = data });
                }

                if (type == "country")
                {
                    var q = _db.Countries.AsQueryable();
                    int recordsTotal = q.Count();
                    if (!string.IsNullOrEmpty(search)) q = q.Where(x => x.Name.Contains(search));
                    int recordsFiltered = q.Count();
                    var data = q.OrderBy(x => x.Name).Skip(start).Take(length).Select(x => new { x.Id, x.Name, x.IsActive }).ToList();
                    return Json(new { draw = draw, recordsTotal = recordsTotal, recordsFiltered = recordsFiltered, data = data });
                }

                // paymentheader
                if (type == "paymentheader")
                {
                    var q = _db.PaymentHeaders.AsQueryable();
                    int recordsTotal = q.Count();
                    if (!string.IsNullOrEmpty(search)) q = q.Where(x => (x.Headers != null && x.Headers.Contains(search)) || (x.HeadersAr != null && x.HeadersAr.Contains(search)));
                    int recordsFiltered = q.Count();
                    var data = q.OrderBy(x => x.Id).ThenBy(x => x.PaymentTypeId).Skip(start).Take(length).Select(x => new { x.Id, x.Headers, x.HeadersAr, x.PaymentTypeId, x.IsActive }).ToList();
                    return Json(new { draw = draw, recordsTotal = recordsTotal, recordsFiltered = recordsFiltered, data = data });
                }

                // default jobtype
                {
                    var q = _db.JobTypes.AsQueryable();
                    int recordsTotal = q.Count();
                    if (!string.IsNullOrEmpty(search)) q = q.Where(x => x.Name.Contains(search));
                    int recordsFiltered = q.Count();
                    var data = q.OrderBy(x => x.Name).Skip(start).Take(length).Select(x => new { x.Id, x.Name, x.IsActive }).ToList();
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
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteItem(string type, int id)
        {
            type = (type ?? "").ToLower();

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
            return Json(new { success = true });
        }
    }
}
