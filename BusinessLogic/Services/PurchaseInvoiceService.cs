using BusinessLogic.Helper;
using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using ClosedXML.Excel;
using Dapper;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using static System.Reflection.Metadata.BlobBuilder;

namespace BusinessLogic.Services
{
    public class PurchaseInvoiceService : IPurchaseInvoice
    {
        private readonly AppDbContext _context;
        private readonly ISessionHelper _session;
        private readonly ILogs _logs;
        private readonly IDatabaseObject _db;
        private readonly IConverter _converter;
        private readonly IAuditService _audit;

        public void Dispose()
        {
            // no-op
        }

        public PurchaseInvoiceService(AppDbContext context, ISessionHelper session, ILogs logs, IDatabaseObject db, IConverter converter, IAuditService audit)
        {
            _context = context;
            _session = session;
            _logs = logs;
            _db = db;
            _converter = converter;
            _audit = audit;
        }

        private async Task<string> GetQrCodeHtml(User user, dynamic inv)
        {
            try
            {
                var setting = await _context.Settings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.SettingKey == "EnableInvoiceQRCode");

                if (setting?.IsActive != true) return "";

                if (!ZatcaQrHelper.IsValidTrn(user.Vatnumber)) return "";

                var invoiceDate = Convert.ToDateTime(inv.InvoiceDate);
                var grandTotal = inv.GrandTotal ?? 0;
                var vatAmount = inv.VatAmount ?? 0;

                return ZatcaQrHelper.GenerateQrImageTag(
                    user.CompanyName ?? "",
                    user.Vatnumber ?? "",
                    invoiceDate,
                    Convert.ToDecimal(grandTotal),
                    Convert.ToDecimal(vatAmount)
                );
            }
            catch
            {
                return "";
            }
        }

        public async Task<dynamic> Save(PurchaseInvoiceVM model, int loginId)
        {
            bool isEdit = model.InvoiceId > 0;
            PurchaseInvoice invoice = null!;
            IDictionary<string, object?>? auditBefore = null;
            int auditLineCountBefore = 0;

            // Regex pattern validation for custom creation format
            var invoiceNoRegex = new Regex(@"^PINV-\d{4}-\d{4}$", RegexOptions.IgnoreCase);

            // ==========================================
            // DATABASE TRANSACTION
            // ==========================================
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Check Duplicate: Same Job ID + Same Service Provider/Vendor (Exclude current record if Editing)
                //var alreadyExists = await _context.Set<PurchaseInvoice>()
                //    .AnyAsync(x => x.JobId == model.JobId
                //                && x.ServiceProviderId == model.ServiceProviderId
                //                && (!isEdit || x.InvoiceId != model.InvoiceId));

                //if (alreadyExists)
                //{
                //    await transaction.RollbackAsync();
                //    return new
                //    {
                //        errorCode = 409,
                //        errorMessage = "An invoice for this Service Provider already exists against the selected Job."
                //    };
                //}

                if (isEdit)
                {
                    // ==========================================
                    // EDIT / UPDATE LOGIC
                    // ==========================================
                    invoice = await _context.Set<PurchaseInvoice>()
                        .FirstOrDefaultAsync(x => x.InvoiceId == model.InvoiceId);

                    if (invoice == null)
                    {
                        await transaction.RollbackAsync();
                        return new { errorCode = 404, errorMessage = "Purchase invoice not found for update." };
                    }

                    // ========== AUDIT: capture state BEFORE any change ==========
                    auditBefore = _audit.Snapshot(invoice);
                    auditLineCountBefore = await _context.Set<PurchaseInvoiceDetail>()
                        .CountAsync(x => x.InvoiceId == model.InvoiceId);

                    // UPDATE ONLY HEADER DATA (InvoiceNo strictly retain hoga)
                    invoice.ServiceProviderId = model.ServiceProviderId;
                    invoice.ServiceProviderName = model.ServiceProviderName;
                    invoice.ServiceProviderAddress = model.ServiceProviderAddress;
                    invoice.ServiceProviderRegion = model.ServiceProviderRegion;
                    invoice.ServiceProviderCountry = model.ServiceProviderCountry;
                    invoice.ServiceProviderVatId = model.ServiceProviderVatId;

                    invoice.JobId = model.JobId;
                    invoice.TotalAmount = model.TotalAmount;
                    invoice.Advance = model.Advance;
                    invoice.Discount = model.Discount;
                    invoice.NetAmount = model.NetAmount;
                    invoice.VatAmount = model.VatAmount;
                    invoice.GrandTotal = model.GrandTotal;
                    invoice.ModifiedBy = loginId;
                    invoice.ModifiedOn = DateTime.Now;

                    _context.Set<PurchaseInvoice>().Update(invoice);

                    // DELETE OLD DETAILS
                    var existingDetails = await _context.Set<PurchaseInvoiceDetail>()
                        .Where(x => x.InvoiceId == model.InvoiceId)
                        .ToListAsync();

                    if (existingDetails.Count > 0)
                    {
                        _context.Set<PurchaseInvoiceDetail>().RemoveRange(existingDetails);
                    }
                }
                else
                {
                    // ==========================================
                    // CREATE LOGIC
                    // ==========================================
                    string finalInvoiceNo = string.Empty;

                    if (!string.IsNullOrWhiteSpace(model.InvoiceNo))
                    {
                        // USER PROVIDED CUSTOM INVOICE NUMBER
                        model.InvoiceNo = model.InvoiceNo.Trim().ToUpper();

                        if (!invoiceNoRegex.IsMatch(model.InvoiceNo))
                        {
                            await transaction.RollbackAsync();
                            return new
                            {
                                errorCode = 400,
                                errorMessage = "Invalid invoice number format. Format must be PINV-2026-0005."
                            };
                        }

                        finalInvoiceNo = model.InvoiceNo;

                        // DUPLICATE INVOICE NO CHECK
                        bool invoiceExists = await _context.Set<PurchaseInvoice>()
                            .AnyAsync(x => x.InvoiceNo == finalInvoiceNo);

                        if (invoiceExists)
                        {
                            await transaction.RollbackAsync();
                            return new { errorCode = 409, errorMessage = "This purchase invoice number already exists." };
                        }

                        // EXTRACT SEQUENCE NUMBER & RE-SEED IF HIGHER
                        var parts = finalInvoiceNo.Split('-');
                        if (parts.Length == 3 && int.TryParse(parts[2], out int customSeqNum))
                        {
                            await UpdatePurchaseSequenceIfHigherAsync(customSeqNum);
                        }
                    }
                    else
                    {
                        // NO INVOICE NUMBER PROVIDED -> GENERATE FROM SQL SEQUENCE
                        var sequenceNumber = await GetNextPurchaseInvoiceSequenceNumber();
                        finalInvoiceNo = $"PINV-{DateTime.Now.Year}-{sequenceNumber:D4}";
                    }

                    // CREATE PURCHASE INVOICE (InvoiceId Identity auto-increment hoga)
                    invoice = new PurchaseInvoice
                    {
                        InvoiceNo = finalInvoiceNo,
                        ServiceProviderId = model.ServiceProviderId,
                        ServiceProviderName = model.ServiceProviderName,
                        ServiceProviderAddress = model.ServiceProviderAddress,
                        ServiceProviderRegion = model.ServiceProviderRegion,
                        ServiceProviderCountry = model.ServiceProviderCountry,
                        ServiceProviderVatId = model.ServiceProviderVatId,
                        JobId = model.JobId,
                        InvoiceDate = DateTime.Now,
                        TotalAmount = model.TotalAmount,
                        Advance = model.Advance,
                        Discount = model.Discount,
                        NetAmount = model.NetAmount,
                        VatAmount = model.VatAmount,
                        GrandTotal = model.GrandTotal,
                        Status = 0,
                        ReferenceId = _session.ReferenceId,
                        CreatedBy = loginId,
                        CreatedOn = DateTime.Now
                    };

                    _context.Set<PurchaseInvoice>().Add(invoice);
                }

                // SAVE HEADER FIRST TO GENERATE IDENTITY InvoiceId
                await _context.SaveChangesAsync();

                // SAVE INVOICE DETAILS
                if (model.Items != null && model.Items.Count > 0)
                {
                    var detailsList = model.Items.Select(item => new PurchaseInvoiceDetail
                    {
                        InvoiceId = invoice.InvoiceId,
                        PaymentTypeId = item.PaymentTypeId,
                        PaymentHeaderId = item.PaymentHeaderId,
                        Qty = item.Qty,
                        Rate = item.Rate,
                        Amount = item.Amount,
                        VatPercent = item.VatPercent,
                        VatAmount = item.VatAmount,
                        TotalAmount = item.TotalAmount
                    });

                    await _context.Set<PurchaseInvoiceDetail>().AddRangeAsync(detailsList);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // ========== AUDIT: record create / update ==========
                var names = await _audit.ResolveNamesAsync(model.ServiceProviderId, model.JobId);
                string providerLabel = names.Lookup.TryGetValue($"ServiceProviderId:{model.ServiceProviderId}", out var spName)
                    ? spName
                    : $"#{model.ServiceProviderId}";
                string jobLabel = names.Lookup.TryGetValue($"JobId:{model.JobId}", out var jNum)
                    ? jNum
                    : $"#{model.JobId}";

                await _audit.RecordAsync(new AuditEntryRequest
                {
                    Module = "Purchase Invoice",
                    Action = isEdit ? "Update" : "Create",
                    EntityName = "PurchaseInvoice",
                    EntityId = invoice.InvoiceNo,
                    Description = isEdit
                        ? $"Purchase invoice {invoice.InvoiceNo} updated for service provider {providerLabel}, job {jobLabel}. Line items: {auditLineCountBefore} -> {model.Items?.Count ?? 0}."
                        : $"Purchase invoice {invoice.InvoiceNo} created for service provider {providerLabel}, job {jobLabel}, total {model.GrandTotal:N2}.",
                    OldValues = auditBefore,
                    NewValues = _audit.Snapshot(invoice),
                    PageName = isEdit ? "/Invoice/EditPurchaseInvoice" : "/Invoice/CreatePurchaseInvoice"
                });
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();

                if (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
                {
                    return new { errorCode = 409, errorMessage = "This purchase invoice number already exists." };
                }
                throw;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            // ==========================================
            // POST TRANSACTION WORK (PDF GENERATION)
            // ==========================================
            try
            {
                var invoiceData = await GetInvoiceById(invoice.InvoiceId, invoice.ServiceProviderId ?? 0);

                if (invoiceData == null)
                {
                    throw new Exception("Invoice data not found for PDF generation");
                }

                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ReferenceId == _session.ReferenceId);

                if (user == null)
                {
                    throw new Exception("User not found.");
                }

                dynamic data = invoiceData;
                var inv = data.invoice;
                var details = data.details;

                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, "wwwroot", "templates", "pdf", "purchaseinvoicenew.html");

                var fullHtml = File.Exists(filePath)
                    ? await File.ReadAllTextAsync(filePath)
                    : "<html><body>Purchase Invoice</body></html>";

                var invoiceRows = new StringBuilder();
                int sr = 1;

                foreach (var item in details)
                {
                    invoiceRows.AppendFormat(
                        "<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td><td>{6}</td><td>{7}</td></tr>",
                        sr++,
                        //item.PaymentType,
                        item.InvoiceDetail,
                        item.Unit,
                        item.Qty,
                        item.Rate,
                        item.Amount,
                        item.VatAmount,
                        item.TotalAmount
                    );
                }

                fullHtml = fullHtml
                    .Replace("{{CompanyName}}", user.CompanyName ?? "")
                    .Replace("{{CompanyNameAr}}", user.CompanyNameAr ?? "")
                    .Replace("{{EstablishmentName}}", user.EstablishmentName ?? "")
                    .Replace("{{EstablishmentNameAr}}", user.EstablishmentNameAr ?? "")
                    .Replace("{{Country}}", user.Country ?? "")
                    .Replace("{{CountryAr}}", user.CountryAr ?? "")
                    .Replace("{{City}}", user.City ?? "")
                    .Replace("{{CityAr}}", user.CityAr ?? "")
                    .Replace("{{VATNumber}}", user.Vatnumber ?? "")
                    .Replace("{{InvoiceNo}}", inv.InvoiceNo ?? "")
                    .Replace("{{InvoiceDate}}", Convert.ToDateTime(inv.InvoiceDate).ToString("dd-MMM-yyyy"))
                    .Replace("{{InvoiceStatus}}", inv.StatusName ?? "")
                    .Replace("{{CustomerName}}", inv.CustomerName ?? "")
                    .Replace("{{CustomerAddress}}", inv.CustomerAddress ?? "")
                    .Replace("{{CustomerCity}}", $"{inv.RegionName} {inv.CountryName}")
                    .Replace("{{CustomerVAT}}", inv.CustomerVat ?? "")
                    .Replace("{{JobNumber}}", inv.JobNumber ?? "")
                    .Replace("{{ModeOfShipment}}", inv.ModeOfShipment ?? "")
                    .Replace("{{ShipmentType}}", inv.ShipmentType ?? "")
                    .Replace("{{POL}}", inv.POL ?? "")
                    .Replace("{{POD}}", inv.POD ?? "")
                    .Replace("{{BLNumber}}", inv.BLNumber ?? "")
                    .Replace("{{PaymentTerms}}", "")
                    .Replace("{{InvoiceRows}}", invoiceRows.ToString())
                    .Replace("{{Total}}", inv.TotalAmount?.ToString("N2") ?? "0")
                    .Replace("{{Advance}}", inv.Advance?.ToString("N2") ?? "0")
                    .Replace("{{Discount}}", inv.Discount?.ToString("N2") ?? "0")
                    .Replace("{{NetAmount}}", inv.NetAmount?.ToString("N2") ?? "0")
                    .Replace("{{VAT}}", inv.VatAmount?.ToString("N2") ?? "0")
                    .Replace("{{GrandTotal}}", inv.GrandTotal?.ToString("N2") ?? "0")
                    .Replace("{{AmountInWords}}", inv.AmountInWords ?? "")

                    .Replace("{{AccountNo}}", user.AccountNo ?? "")
                    .Replace("{{AccountTitle}}", user.AccountTitle ?? "")
                    .Replace("{{BankName}}", user.BankName ?? "")
                    .Replace("{{Branch}}", user.Branch ?? "")
                    .Replace("{{SwiftCode}}", user.SwiftCode ?? "")
                    .Replace("{{Iban}}", user.Iban ?? "")

                    .Replace("{{PrintDate}}", DateTime.Now.ToString("dd-MMM-yyyy hh:mm tt"))
                    .Replace("{{QRCodeValue}}", await GetQrCodeHtml(user, inv));

                var pdfBytes = GeneratePdfFromHtml(fullHtml);

                var pdfFolder = Path.Combine(basePath, "wwwroot", "pdfs", "purchase");
                if (!Directory.Exists(pdfFolder))
                {
                    Directory.CreateDirectory(pdfFolder);
                }

                var pdfPath = Path.Combine(pdfFolder, $"{invoice.InvoiceNo}.pdf");
                await File.WriteAllBytesAsync(pdfPath, pdfBytes);

                return new
                {
                    errorCode = 200,
                    message = isEdit ? "Purchase Invoice Updated Successfully" : "Purchase Invoice Saved Successfully",
                    invoiceId = invoice.InvoiceId,
                    invoiceNo = invoice.InvoiceNo
                };
            }
            catch (Exception ex)
            {
                _logs.Write("PurchaseInvoice", "Save", ex.ToString());

                return new
                {
                    errorCode = 500,
                    errorMessage = ex.InnerException?.Message ?? ex.Message
                };
            }
        }

        public async Task<dynamic> Save_Bk2(PurchaseInvoiceVM model, int loginId)
        {
            bool isEdit = model.InvoiceId > 0;
            PurchaseInvoice invoice = null!;

            // 1. DATABASE TRANSACTION (Scope kept minimal for fast DB locks release)
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Check Duplicate Job ID (Exclude current record if Editing)
                    var alreadyExists = await _context.Set<PurchaseInvoice>()
                        .AnyAsync(x => x.JobId == model.JobId && (!isEdit || x.InvoiceId != model.InvoiceId));

                    if (alreadyExists)
                    {
                        return new
                        {
                            errorCode = 409,
                            errorMessage = "Invoice already created against this job."
                        };
                    }

                    if (isEdit)
                    {
                        // ==========================================
                        // EDIT / UPDATE LOGIC
                        // ==========================================
                        invoice = await _context.Set<PurchaseInvoice>()
                            .FirstOrDefaultAsync(x => x.InvoiceId == model.InvoiceId);

                        if (invoice == null)
                        {
                            return new
                            {
                                errorCode = 404,
                                errorMessage = "Invoice not found for update."
                            };
                        }

                        // Update Header Fields
                        invoice.ServiceProviderId = model.ServiceProviderId;
                        invoice.ServiceProviderName = model.ServiceProviderName;
                        invoice.ServiceProviderAddress = model.ServiceProviderAddress;
                        invoice.ServiceProviderRegion = model.ServiceProviderRegion;
                        invoice.ServiceProviderCountry = model.ServiceProviderCountry;
                        invoice.ServiceProviderVatId = model.ServiceProviderVatId;

                        invoice.JobId = model.JobId;
                        invoice.TotalAmount = model.TotalAmount;
                        invoice.Advance = model.Advance;
                        invoice.Discount = model.Discount;
                        invoice.NetAmount = model.NetAmount;
                        invoice.VatAmount = model.VatAmount;
                        invoice.GrandTotal = model.GrandTotal;
                        invoice.ModifiedBy = loginId;
                        invoice.ModifiedOn = DateTime.Now;

                        _context.Set<PurchaseInvoice>().Update(invoice);

                        // Remove existing line items to sync fresh data
                        var existingDetails = await _context.Set<PurchaseInvoiceDetail>()
                            .Where(x => x.InvoiceId == model.InvoiceId)
                            .ToListAsync();

                        if (existingDetails.Count > 0)
                        {
                            _context.Set<PurchaseInvoiceDetail>().RemoveRange(existingDetails);
                        }
                    }
                    else
                    {
                        // ==========================================
                        // CREATE LOGIC
                        // ==========================================
                        var maxId = await _context.PurchaseInvoices.MaxAsync(c => (int?)c.InvoiceId) ?? 0;
                        int newId = maxId + 1;

                        string invoiceNo = $"PINV-{DateTime.Now.Year}-{newId:D4}";

                        invoice = new PurchaseInvoice
                        {
                            InvoiceId = newId,
                            InvoiceNo = invoiceNo,
                            ServiceProviderId = model.ServiceProviderId,
                            ServiceProviderName = model.ServiceProviderName,
                            ServiceProviderAddress = model.ServiceProviderAddress,
                            ServiceProviderRegion = model.ServiceProviderRegion,
                            ServiceProviderCountry = model.ServiceProviderCountry,
                            ServiceProviderVatId = model.ServiceProviderVatId,
                            JobId = model.JobId,
                            InvoiceDate = DateTime.Now,
                            TotalAmount = model.TotalAmount,
                            Advance = model.Advance,
                            Discount = model.Discount,
                            NetAmount = model.NetAmount,
                            VatAmount = model.VatAmount,
                            GrandTotal = model.GrandTotal,
                            Status = 0,
                            ReferenceId = _session.ReferenceId,
                            CreatedBy = loginId,
                            CreatedOn = DateTime.Now
                        };

                        _context.Set<PurchaseInvoice>().Add(invoice);
                    }

                    // Save Details using AddRange for single bulk insertion
                    if (model.Items != null && model.Items.Count > 0)
                    {
                        var detailsList = model.Items.Select(item => new PurchaseInvoiceDetail
                        {
                            InvoiceId = invoice.InvoiceId,
                            PaymentTypeId = item.PaymentTypeId,
                            PaymentHeaderId = item.PaymentHeaderId,
                            Qty = item.Qty,
                            Rate = item.Rate,
                            Amount = item.Amount,
                            VatPercent = item.VatPercent,
                            VatAmount = item.VatAmount,
                            TotalAmount = item.TotalAmount
                        });

                        await _context.Set<PurchaseInvoiceDetail>().AddRangeAsync(detailsList);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw; // Rethrow to outer catch block for unified handling
                }
            }

            // 2. POST-TRANSACTION WORK (PDF Generation & Processing)
            try
            {
                var invoiceData = await GetInvoiceById(invoice.InvoiceId, invoice.ServiceProviderId ?? 0);

                if (invoiceData == null)
                {
                    throw new Exception("Invoice data not found for PDF generation");
                }

                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ReferenceId == _session.ReferenceId);

                if (user == null)
                {
                    throw new Exception("User not found.");
                }

                dynamic data = invoiceData;
                var inv = data.invoice;
                var details = data.details;

                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, "wwwroot", "templates", "pdf", "purchaseinvoice.html");

                var fullHtml = File.Exists(filePath)
                    ? await File.ReadAllTextAsync(filePath)
                    : "<html><body>Purchase Invoice</body></html>";

                // Memory efficient string concatenation
                var invoiceRows = new StringBuilder();
                int sr = 1;

                foreach (var item in details)
                {
                    invoiceRows.AppendFormat(
                        "<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td><td>{6}</td><td>{7}</td><td>{8}</td></tr>",
                        sr++,
                        item.PaymentType,
                        item.InvoiceDetail,
                        item.Unit,
                        item.Qty,
                        item.Rate,
                        item.Amount,
                        item.VatAmount,
                        item.TotalAmount
                    );
                }

                fullHtml = fullHtml
                    .Replace("{{CompanyName}}", user.CompanyName ?? "")
                    .Replace("{{CompanyNameAr}}", user.CompanyNameAr ?? "")
                    .Replace("{{EstablishmentName}}", user.EstablishmentName ?? "")
                    .Replace("{{EstablishmentNameAr}}", user.EstablishmentNameAr ?? "")
                    .Replace("{{Country}}", user.Country ?? "")
                    .Replace("{{CountryAr}}", user.CountryAr ?? "")
                    .Replace("{{City}}", user.City ?? "")
                    .Replace("{{CityAr}}", user.CityAr ?? "")
                    .Replace("{{VATNumber}}", user.Vatnumber ?? "")
                    .Replace("{{InvoiceNo}}", inv.InvoiceNo ?? "")
                    .Replace("{{InvoiceDate}}", Convert.ToDateTime(inv.InvoiceDate).ToString("dd-MMM-yyyy"))
                    .Replace("{{InvoiceStatus}}", inv.StatusName ?? "")
                    .Replace("{{CustomerName}}", inv.CustomerName ?? "")
                    .Replace("{{CustomerAddress}}", inv.CustomerAddress ?? "")
                    .Replace("{{CustomerCity}}", $"{inv.RegionName} {inv.CountryName}")
                    .Replace("{{CustomerVAT}}", inv.CustomerVat ?? "")
                    .Replace("{{JobNumber}}", inv.JobNumber ?? "")
                    .Replace("{{ModeOfShipment}}", inv.ModeOfShipment ?? "")
                    .Replace("{{ShipmentType}}", inv.ShipmentType ?? "")
                    .Replace("{{POL}}", inv.POL ?? "")
                    .Replace("{{POD}}", inv.POD ?? "")
                    .Replace("{{BLNumber}}", inv.BLNumber ?? "")
                    .Replace("{{PaymentTerms}}", "")
                    .Replace("{{InvoiceRows}}", invoiceRows.ToString())
                    .Replace("{{Total}}", inv.TotalAmount?.ToString("N2") ?? "0")
                    .Replace("{{Advance}}", inv.Advance?.ToString("N2") ?? "0")
                    .Replace("{{Discount}}", inv.Discount?.ToString("N2") ?? "0")
                    .Replace("{{NetAmount}}", inv.NetAmount?.ToString("N2") ?? "0")
                    .Replace("{{VAT}}", inv.VatAmount?.ToString("N2") ?? "0")
                    .Replace("{{GrandTotal}}", inv.GrandTotal?.ToString("N2") ?? "0")
                    .Replace("{{AmountInWords}}", inv.AmountInWords ?? "")
                    .Replace("{{AccountNo}}", inv.AccountNumber ?? "")
                    .Replace("{{BankName}}", inv.BankName ?? "")
                    .Replace("{{IBAN}}", inv.IBAN ?? "")
                    .Replace("{{PrintDate}}", DateTime.Now.ToString("dd-MMM-yyyy hh:mm tt"))
                    .Replace("{{QRCodeValue}}", await GetQrCodeHtml(user, inv));

                // Generate and Overwrite PDF
                var pdfBytes = GeneratePdfFromHtml(fullHtml);

                var pdfFolder = Path.Combine(basePath, "wwwroot", "pdfs", "purchase");
                if (!Directory.Exists(pdfFolder))
                {
                    Directory.CreateDirectory(pdfFolder);
                }

                var pdfPath = Path.Combine(pdfFolder, $"{invoice.InvoiceNo}.pdf");
                await File.WriteAllBytesAsync(pdfPath, pdfBytes);

                return new
                {
                    errorCode = 200,
                    message = isEdit ? "Invoice Updated Successfully" : "Invoice Saved Successfully",
                    invoiceId = invoice.InvoiceId,
                    invoiceNo = invoice.InvoiceNo
                };
            }
            catch (Exception ex)
            {
                _logs.Write("PurchaseInvoice", "Save", ex.ToString());

                return new
                {
                    errorCode = 500,
                    errorMessage = ex.InnerException?.Message ?? ex.Message
                };
            }
        }

        public async Task<dynamic> Save_Bk(PurchaseInvoiceVM model, int loginId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var alreadyExists = await _context.Set<PurchaseInvoice>()
                    .AnyAsync(x => x.JobId == model.JobId);

                if (alreadyExists)
                {
                    return new
                    {
                        errorCode = 409,
                        errorMessage = "Invoice already created against this job."
                    };
                }

                //int count = await _context.Set<PurchaseInvoice>().CountAsync();

                var maxId = await _context.PurchaseInvoices.MaxAsync(c => (int?)c.InvoiceId) ?? 0;
                int newId = maxId + 1;

                string invoiceNo =
                    "PINV-" + DateTime.Now.Year + "-" + (newId).ToString("D4");

                var invoice = new PurchaseInvoice
                {
                    InvoiceId = newId,
                    InvoiceNo = invoiceNo,
                    ServiceProviderId = model.ServiceProviderId,

                    ServiceProviderName = model.ServiceProviderName,
                    ServiceProviderAddress = model.ServiceProviderAddress,
                    ServiceProviderRegion = model.ServiceProviderRegion,
                    ServiceProviderCountry = model.ServiceProviderCountry,
                    ServiceProviderVatId = model.ServiceProviderVatId,

                    JobId = model.JobId,
                    InvoiceDate = DateTime.Now,
                    TotalAmount = model.TotalAmount,
                    Advance = model.Advance,
                    Discount = model.Discount,
                    NetAmount = model.NetAmount,
                    VatAmount = model.VatAmount,
                    GrandTotal = model.GrandTotal,

                    Status = 0,
                    ReferenceId = _session.ReferenceId,
                    CreatedBy = loginId,
                    CreatedOn = DateTime.Now
                };

                _context.Set<PurchaseInvoice>().Add(invoice);
                await _context.SaveChangesAsync();

                foreach (var item in model.Items)
                {
                    _context.Set<PurchaseInvoiceDetail>().Add(new PurchaseInvoiceDetail
                    {
                        InvoiceId = invoice.InvoiceId,
                        PaymentTypeId = item.PaymentTypeId,
                        PaymentHeaderId = item.PaymentHeaderId,
                        Qty = item.Qty,
                        Rate = item.Rate,
                        Amount = item.Amount,
                        VatPercent = item.VatPercent,
                        VatAmount = item.VatAmount,
                        TotalAmount = item.TotalAmount
                    });
                }

                await _context.SaveChangesAsync();

                // fetch full data using existing GetInvoiceById logic (purchase)
                var invoiceData = await GetInvoiceById(invoice.InvoiceId, invoice.ServiceProviderId ?? 0);

                if (invoiceData == null)
                {
                    throw new Exception("Invoice data not found for PDF generation");
                }

                dynamic data = invoiceData;

                var inv = data.invoice;
                var details = data.details;

                var basePath = Directory.GetCurrentDirectory();

                var filePath = Path.Combine(basePath, "wwwroot", "templates", "pdf", "purchaseinvoice.html");

                var fullHtml = File.Exists(filePath) ? await File.ReadAllTextAsync(filePath) : "<html><body>Purchase Invoice</body></html>";

                string invoiceRows = "";
                int sr = 1;

                foreach (var item in details)
                {
                    invoiceRows += $@"
                    <tr>
                        <td>{sr}</td>
                        <td>{item.PaymentType}</td>
                        <td>{item.InvoiceDetail}</td>
                        <td>{item.Unit}</td>
                        <td>{item.Qty}</td>
                        <td>{item.Rate}</td>
                        <td>{item.Amount}</td>
                        <td>{item.VatAmount}</td>
                        <td>{item.TotalAmount}</td>
                    </tr>";

                    sr++;
                }

                fullHtml = fullHtml.Replace("{{InvoiceNo}}", inv.InvoiceNo ?? "");
                fullHtml = fullHtml.Replace("{{InvoiceDate}}", Convert.ToDateTime(inv.InvoiceDate).ToString("dd-MMM-yyyy"));
                fullHtml = fullHtml.Replace("{{InvoiceStatus}}", inv.StatusName ?? "");

                fullHtml = fullHtml.Replace("{{CustomerName}}", inv.CustomerName ?? "");
                fullHtml = fullHtml.Replace("{{CustomerAddress}}", inv.CustomerAddress ?? "");
                fullHtml = fullHtml.Replace("{{CustomerCity}}", inv.RegionName + " " + inv.CountryName);
                fullHtml = fullHtml.Replace("{{CustomerVAT}}", inv.CustomerVat ?? "");

                fullHtml = fullHtml.Replace("{{JobNumber}}", inv.JobNumber ?? "");
                fullHtml = fullHtml.Replace("{{ModeOfShipment}}", inv.ModeOfShipment ?? "");
                fullHtml = fullHtml.Replace("{{ShipmentType}}", inv.ShipmentType ?? "");
                fullHtml = fullHtml.Replace("{{POL}}", inv.POL ?? "");
                fullHtml = fullHtml.Replace("{{POD}}", inv.POD ?? "");
                fullHtml = fullHtml.Replace("{{BLNumber}}", inv.BLNumber ?? "");

                fullHtml = fullHtml.Replace("{{PaymentTerms}}", "");
                fullHtml = fullHtml.Replace("{{InvoiceRows}}", invoiceRows);

                fullHtml = fullHtml.Replace("{{Total}}", inv.TotalAmount?.ToString("N2") ?? "0");
                fullHtml = fullHtml.Replace("{{Advance}}", inv.Advance?.ToString("N2") ?? "0");
                fullHtml = fullHtml.Replace("{{Discount}}", inv.Discount?.ToString("N2") ?? "0");
                fullHtml = fullHtml.Replace("{{NetAmount}}", inv.NetAmount?.ToString("N2") ?? "0");
                fullHtml = fullHtml.Replace("{{VAT}}", inv.VatAmount?.ToString("N2") ?? "0");
                fullHtml = fullHtml.Replace("{{GrandTotal}}", inv.GrandTotal?.ToString("N2") ?? "0");

                fullHtml = fullHtml.Replace("{{AmountInWords}}", inv.AmountInWords ?? "");

                fullHtml = fullHtml.Replace("{{AccountNo}}", inv.AccountNumber ?? "");
                fullHtml = fullHtml.Replace("{{BankName}}", inv.BankName ?? "");
                fullHtml = fullHtml.Replace("{{IBAN}}", inv.IBAN ?? "");

                fullHtml = fullHtml.Replace("{{PrintDate}}", DateTime.Now.ToString("dd-MMM-yyyy hh:mm tt"));

                var qrUser = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ReferenceId == _session.ReferenceId);
                fullHtml = fullHtml.Replace("{{QRCodeValue}}", await GetQrCodeHtml(qrUser!, inv));

                var pdfBytes = GeneratePdfFromHtml(fullHtml);

                var pdfFolder = Path.Combine(basePath, "wwwroot", "pdfs", "purchase");
                if (!Directory.Exists(pdfFolder))
                    Directory.CreateDirectory(pdfFolder);

                var pdfPath = Path.Combine(pdfFolder, invoiceNo + ".pdf");
                await File.WriteAllBytesAsync(pdfPath, pdfBytes);

                await transaction.CommitAsync();

                return new
                {
                    errorCode = 200,
                    message = "Invoice Saved Successfully",
                    invoiceId = invoice.InvoiceId,
                    invoiceNo = invoice.InvoiceNo
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                _logs.Write(
                    "PurchaseInvoice",
                    "Save",
                    ex.InnerException?.Message ?? ex.Message
                );

                return new
                {
                    errorCode = 500,
                    errorMessage = ex.InnerException?.Message ?? ex.Message
                };
            }
        }

        public async Task<dynamic> GetInvoices(List<QueryFilters> filters)
        {
            try
            {
                int draw = Convert.ToInt32(filters.First(x => x.fieldName == "draw").filterValue);
                int start = Convert.ToInt32(filters.First(x => x.fieldName == "start").filterValue);
                int length = Convert.ToInt32(filters.First(x => x.fieldName == "length").filterValue);

                string search = filters.First(x => x.fieldName == "search").filterValue?.ToString();

                string invoiceNo = filters.First(x => x.fieldName == "InvoiceNo").filterValue?.ToString();


                int jobId = 0;
                int.TryParse(filters.First(x => x.fieldName == "JobNo").filterValue, out jobId);

                int serviceProviderId = 0;
                int.TryParse(filters.First(x => x.fieldName == "ServiceProviderId").filterValue, out serviceProviderId);

                DateTime? fromDate = null;
                DateTime? toDate = null;

                if (DateTime.TryParse(filters.First(x => x.fieldName == "FromDate").filterValue, out DateTime f))
                    fromDate = f;

                if (DateTime.TryParse(filters.First(x => x.fieldName == "ToDate").filterValue, out DateTime t))
                    toDate = t;

                var query =
                    from inv in _context.Set<PurchaseInvoice>()

                    join c in _context.Customers
                        on inv.ServiceProviderId equals c.Id into custJoin
                    from c in custJoin.DefaultIfEmpty()

                    join jm in _context.JobImportMasters
                        on inv.JobId equals jm.Id into jobJoin
                    from jm in jobJoin.DefaultIfEmpty()

                    join u in _context.Users
                        on inv.CreatedBy equals u.Id into userJoin
                    from u in userJoin.DefaultIfEmpty()

                    select new
                    {
                        inv.InvoiceId,
                        inv.InvoiceNo,
                        JobId = inv.JobId,
                        JobNo = jm.JobNumber,
                        BlNo = jm.BlNo,
                        ServiceProviderId = inv.ServiceProviderId,
                        ServiceProviderName = inv.ServiceProviderId > 0 && c != null ? c.CustomerName : inv.ServiceProviderName,
                        inv.InvoiceDate,
                        inv.NetAmount,
                        inv.TotalAmount,
                        inv.VatAmount,
                        inv.GrandTotal,
                        inv.PaidAmount,
                        inv.BalanceAmount,
                        inv.Status,
                        inv.IsCancelled,
                        inv.ReferenceId,
                        StatusName = StatusHelper.GetInvoiceStatus(0),
                        CreatedByUser = u.UserName
                    };


                query = query.Where(x => x.ReferenceId == _session.ReferenceId);

                if (!string.IsNullOrEmpty(invoiceNo))
                    query = query.Where(x => x.InvoiceNo.Contains(invoiceNo));

                if (jobId > 0)
                    query = query.Where(x => x.JobId == jobId);

                if (serviceProviderId > 0)
                    query = query.Where(x => x.ServiceProviderId == serviceProviderId);

                if (fromDate.HasValue)
                    query = query.Where(x => x.InvoiceDate >= fromDate);

                if (toDate.HasValue)
                    query = query.Where(x => x.InvoiceDate <= toDate);

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(x =>
                        x.InvoiceNo.Contains(search) ||
                        x.JobNo.Contains(search) ||
                        x.ServiceProviderName.Contains(search));
                }

                int totalRecords = await query.CountAsync();

                var data = await query
                    .OrderByDescending(x => x.InvoiceId)
                    .Skip(start)
                    .Take(length)
                    .ToListAsync();

                return new
                {
                    draw = draw,
                    recordsTotal = totalRecords,
                    recordsFiltered = totalRecords,
                    data = data
                };
            }
            catch (Exception ex)
            {
                _logs.Write("PurchaseInvoice", "GetInvoices",
                    ex.InnerException?.Message ?? ex.Message);

                return new
                {
                    draw = 0,
                    recordsTotal = 0,
                    recordsFiltered = 0,
                    data = new List<object>()
                };
            }
        }

        private async Task<List<dynamic>> GetExportData(IList<QueryFilters> filters)
        {
            string invoiceNo = filters.FirstOrDefault(x => x.fieldName == "InvoiceNo")?.filterValue?.ToString();

            int jobId = 0;
            int.TryParse(filters.FirstOrDefault(x => x.fieldName == "JobNo")?.filterValue, out jobId);

            int serviceProviderId = 0;
            int.TryParse(filters.FirstOrDefault(x => x.fieldName == "ServiceProviderId")?.filterValue, out serviceProviderId);

            DateTime? fromDate = null;
            DateTime? toDate = null;

            if (DateTime.TryParse(filters.FirstOrDefault(x => x.fieldName == "FromDate")?.filterValue, out DateTime f))
                fromDate = f;

            if (DateTime.TryParse(filters.FirstOrDefault(x => x.fieldName == "ToDate")?.filterValue, out DateTime t))
                toDate = t;

            var query =
                from inv in _context.Set<PurchaseInvoice>()

                join c in _context.Customers
                    on inv.ServiceProviderId equals c.Id into custJoin
                from c in custJoin.DefaultIfEmpty()

                join jm in _context.JobImportMasters
                    on inv.JobId equals jm.Id into jobJoin
                from jm in jobJoin.DefaultIfEmpty()

                join u in _context.Users
                    on inv.CreatedBy equals u.Id into userJoin
                from u in userJoin.DefaultIfEmpty()

                select new
                {
                    inv.InvoiceId,
                    inv.InvoiceNo,
                    JobId = inv.JobId,
                    JobNo = jm.JobNumber,
                    ServiceProviderId = inv.ServiceProviderId,
                    ServiceProviderName = inv.ServiceProviderId > 0 && c != null ? c.CustomerName : inv.ServiceProviderName,
                    inv.InvoiceDate,
                    inv.NetAmount,
                    inv.VatAmount,
                    inv.GrandTotal,
                    inv.PaidAmount,
                    inv.BalanceAmount,
                    inv.Status,
                    StatusName = StatusHelper.GetInvoiceStatus(inv.Status),
                    CreatedByUser = u.UserName,
                    inv.ReferenceId
                };

            query = query.Where(x => x.ReferenceId == _session.ReferenceId);

            if (!string.IsNullOrEmpty(invoiceNo))
                query = query.Where(x => x.InvoiceNo.Contains(invoiceNo));

            if (jobId > 0)
                query = query.Where(x => x.JobId == jobId);

            if (serviceProviderId > 0)
                query = query.Where(x => x.ServiceProviderId == serviceProviderId);

            if (fromDate.HasValue)
                query = query.Where(x => x.InvoiceDate >= fromDate);

            if (toDate.HasValue)
                query = query.Where(x => x.InvoiceDate <= toDate);

            var rows = await query
                .OrderByDescending(x => x.InvoiceId)
                .ToListAsync();

            return rows.Cast<dynamic>().ToList();
        }

        public async Task<byte[]> ExportExcel(IList<QueryFilters> filters)
        {
            var rows = await GetExportData(filters);

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Purchase Invoices");

                var navyColor = XLColor.FromHtml("#1F4E78");

                ws.Cell("A1").Value = "PURCHASE INVOICE REPORT";
                ws.Cell("A1").Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(navyColor);

                string[] headers = {
                    "Invoice No", "Job No", "Service Provider", "Invoice Date",
                    "Net Amount", "VAT", "Grand Total", "Paid Amount", "Balance",
                    "Status", "Created By"
                };

                var headerRange = ws.Range(3, 1, 3, headers.Length);
                headerRange.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(navyColor);
                headerRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                for (int i = 0; i < headers.Length; i++)
                    ws.Cell(3, i + 1).Value = headers[i];

                int row = 4;

                foreach (var r in rows)
                {
                    ws.Cell(row, 1).Value = r.InvoiceNo;
                    ws.Cell(row, 2).Value = r.JobNo;

                    ws.Cell(row, 4).Value = ((DateTime)r.InvoiceDate).ToString("dd/MM/yyyy");
                    ws.Cell(row, 3).Value = r.ServiceProviderName;

                    ws.Cell(row, 5).Value = (decimal)r.NetAmount;
                    ws.Cell(row, 6).Value = (decimal)r.VatAmount;
                    ws.Cell(row, 7).Value = (decimal)r.GrandTotal;
                    ws.Cell(row, 8).Value = (decimal)(r.PaidAmount ?? 0m);
                    ws.Cell(row, 9).Value = (decimal)(r.BalanceAmount ?? 0m);
                    ws.Cell(row, 10).Value = r.StatusName;
                    ws.Cell(row, 11).Value = r.CreatedByUser;

                    for (int c = 5; c <= 9; c++)
                        ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.00";

                    row++;
                }

                ws.Columns().AdjustToContents(5, 50);
                ws.SheetView.FreezeRows(3);

                using (var ms = new MemoryStream())
                {
                    workbook.SaveAs(ms);
                    return ms.ToArray();
                }
            }
        }

        public async Task<byte[]> ExportPdf(IList<QueryFilters> filters)
        {
            var rows = await GetExportData(filters);

            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
            sb.AppendLine("<style>");
            sb.AppendLine("body{font-family:'Segoe UI',Arial,sans-serif;font-size:9px;color:#212529;}");
            sb.AppendLine("h2{color:#1F4E78;margin:0 0 4px 0;}");
            sb.AppendLine(".filter-line{font-size:9px;color:#595959;margin-bottom:12px;}");
            sb.AppendLine("table{width:100%;border-collapse:collapse;}");
            sb.AppendLine("th,td{border:1px solid #999;padding:4px 6px;text-align:left;}");
            sb.AppendLine("th{background:#1F4E78;color:#fff;font-weight:bold;}");
            sb.AppendLine("tr:nth-child(even){background:#F9FAFB;}");
            sb.AppendLine(".text-end{text-align:right;}");
            sb.AppendLine("</style></head><body>");

            sb.AppendLine("<h2>PURCHASE INVOICE REPORT</h2>");

            sb.AppendLine("<table><thead><tr>");
            sb.AppendLine("<th>Invoice No</th><th>Job No</th><th>Service Provider</th><th>Invoice Date</th>");
            sb.AppendLine("<th class='text-end'>Net Amount</th><th class='text-end'>VAT</th><th class='text-end'>Grand Total</th>");
            sb.AppendLine("<th class='text-end'>Paid Amount</th><th class='text-end'>Balance</th><th>Status</th><th>Created By</th>");
            sb.AppendLine("</tr></thead><tbody>");

            decimal totalNet = 0, totalVat = 0, totalGrand = 0, totalPaid = 0;

            foreach (var r in rows)
            {
                sb.Append("<tr>");
                sb.Append($"<td>{r.InvoiceNo}</td>");
                sb.Append($"<td>{r.JobNo}</td>");
                sb.Append($"<td>{r.ServiceProviderName}</td>");
                sb.Append($"<td>{((DateTime)r.InvoiceDate).ToString("dd/MM/yyyy")}</td>");
                sb.Append($"<td class='text-end'>{((decimal)r.NetAmount):N2}</td>");
                sb.Append($"<td class='text-end'>{((decimal)r.VatAmount):N2}</td>");
                sb.Append($"<td class='text-end'>{((decimal)r.GrandTotal):N2}</td>");
                sb.Append($"<td class='text-end'>{((decimal)(r.PaidAmount ?? 0m)):N2}</td>");
                sb.Append($"<td class='text-end'>{((decimal)(r.BalanceAmount ?? 0m)):N2}</td>");
                sb.Append($"<td>{r.StatusName}</td>");
                sb.Append($"<td>{r.CreatedByUser}</td>");
                sb.Append("</tr>");

                totalNet += (decimal)r.NetAmount;
                totalVat += (decimal)r.VatAmount;
                totalGrand += (decimal)r.GrandTotal;
                totalPaid += (decimal)(r.PaidAmount ?? 0m);
            }

            sb.Append("<tr style='font-weight:bold;background:#EAEEF3;'>");
            sb.Append("<td colspan='4'>TOTAL</td>");
            sb.Append($"<td class='text-end'>{totalNet:N2}</td>");
            sb.Append($"<td class='text-end'>{totalVat:N2}</td>");
            sb.Append($"<td class='text-end'>{totalGrand:N2}</td>");
            sb.Append($"<td class='text-end'>{totalPaid:N2}</td>");
            sb.Append("<td></td><td></td><td></td>");
            sb.Append("</tr>");

            sb.AppendLine("</tbody></table></body></html>");

            var doc = new HtmlToPdfDocument
            {
                GlobalSettings =
                {
                    PaperSize = PaperKind.A4,
                    Orientation = Orientation.Landscape,
                    Margins = { Top = 8, Bottom = 8, Left = 5, Right = 5 }
                },
                Objects =
                {
                    new ObjectSettings
                    {
                        HtmlContent = sb.ToString(),
                        WebSettings =
                        {
                            DefaultEncoding = "utf-8",
                            LoadImages = true,
                            PrintMediaType = true
                        }
                    }
                }
            };

            return _converter.Convert(doc);
        }

        public async Task<dynamic> GetInvoiceById(int invoiceId, int serviceProviderId, int? ReferenceId = null)
        {
            try
            {
                var invoice_check = await _context.Set<PurchaseInvoice>()
                                             .FirstOrDefaultAsync(c => c.InvoiceId == invoiceId);

                if (invoice_check == null)
                {
                    return new
                    {
                        errorCode = 404,
                        errorMessage = "Invoice not found"
                    };
                }

                var invoice = (object) null;

                if (serviceProviderId > 0)
                {

                    invoice =
                        await (
                            from inv in _context.Set<PurchaseInvoice>()

                            join c in _context.Customers
                                on inv.ServiceProviderId equals (int?)c.Id

                            join r in _context.Regions
                                on c.RegionId equals r.Id into regionJoin
                            from r in regionJoin.DefaultIfEmpty()

                            join co in _context.Countries
                                on c.CountryId equals co.Id into countryJoin
                            from co in countryJoin.DefaultIfEmpty()

                            join jm in _context.JobImportMasters
                                on inv.JobId equals jm.Id

                            join pol in _context.Pols
                                on jm.PolId equals pol.Id into polJoin
                            from pol in polJoin.DefaultIfEmpty()

                            join pod in _context.Pods
                                on jm.PodId equals pod.Id into podJoin
                            from pod in podJoin.DefaultIfEmpty()

                            join sm in _context.ShipmentModes
                                on jm.ShipmentModeId equals sm.Id into smJoin
                            from sm in smJoin.DefaultIfEmpty()

                            where inv.InvoiceId == invoiceId

                            select new
                            {
                                inv.InvoiceId,
                                inv.InvoiceNo,
                                inv.InvoiceDate,
                                inv.Status,
                                StatusName = StatusHelper.GetInvoiceStatus(inv.Status),

                                inv.TotalAmount,
                                inv.Advance,
                                inv.Discount,
                                inv.NetAmount,
                                inv.VatAmount,
                                inv.GrandTotal,

                                CustomerName = c.CustomerName,
                                CustomerAddress = c.Address,

                                RegionName = r.Name,
                                CountryName = co.Name,

                                CustomerVat = c.VatId,

                                BankName = c.BankName,
                                AccountName = c.AccountNumber,
                                AccountNumber = c.AccountNumber,
                                IBAN = c.Iban,
                                SwiftCode = c.SwiftCode,

                                JobNumber = jm.JobNumber,

                                ShipmentType =
                                    jm.ShipmentType == 1
                                    ? "Import"
                                    : "Export",

                                ModeOfShipment = sm.Name,

                                POL = pol.Name,
                                POD = pod.Name,

                                BLNumber = jm.BlNo,

                                AmountInWords = AmountInWordsHelper.NumberToWords(inv.GrandTotal)
                            }

                        ).FirstOrDefaultAsync();
                } 
                else
                {
                   invoice =
                        await (
                            from inv in _context.Set<PurchaseInvoice>()

                            join jm in _context.JobImportMasters
                                on inv.JobId equals jm.Id

                            join pol in _context.Pols
                                on jm.PolId equals pol.Id into polJoin
                            from pol in polJoin.DefaultIfEmpty()

                            join pod in _context.Pods
                                on jm.PodId equals pod.Id into podJoin
                            from pod in podJoin.DefaultIfEmpty()

                            join sm in _context.ShipmentModes
                                on jm.ShipmentModeId equals sm.Id into smJoin
                            from sm in smJoin.DefaultIfEmpty()

                            where inv.InvoiceId == invoiceId

                            select new
                            {
                                inv.InvoiceId,
                                inv.InvoiceNo,
                                inv.InvoiceDate,
                                inv.Status,
                                StatusName = StatusHelper.GetInvoiceStatus(inv.Status),
                                inv.TotalAmount,
                                inv.Advance,
                                inv.Discount,
                                inv.NetAmount,
                                inv.VatAmount,
                                inv.GrandTotal,

                                CustomerName = inv.ServiceProviderName,
                                CustomerAddress = inv.ServiceProviderAddress,

                                RegionName = inv.ServiceProviderRegion,
                                CountryName = inv.ServiceProviderCountry,

                                CustomerVat = inv.ServiceProviderVatId,

                                BankName = "",
                                AccountName = "",
                                AccountNumber = "",
                                IBAN = "",
                                SwiftCode = "",

                                JobNumber = jm.JobNumber,

                                ShipmentType =
                                    jm.ShipmentType == 1
                                    ? "Import"
                                    : "Export",

                                ModeOfShipment = sm.Name,

                                POL = pol.Name,
                                POD = pod.Name,

                                BLNumber = jm.BlNo,

                                AmountInWords = AmountInWordsHelper.NumberToWords(inv.GrandTotal)
                            }

                        ).FirstOrDefaultAsync();
                }

                var details =
                    await (
                        from d in _context.Set<PurchaseInvoiceDetail>()

                        join ph in _context.PaymentHeaders
                            on d.PaymentHeaderId equals ph.Id

                        where d.InvoiceId == invoiceId

                        select new
                        {
                            d.Id,

                            PaymentType =
                                d.PaymentTypeId == 1
                                ? "Official"
                                : "Unofficial",

                            InvoiceDetail = ph.Headers,

                            Unit = 1,

                            d.Qty,
                            d.Rate,
                            d.Amount,

                            d.VatAmount,
                            d.VatPercent,
                            d.TotalAmount
                        }

                    ).ToListAsync();

                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ReferenceId == (ReferenceId ?? _session.ReferenceId));

                if (user == null)
                {
                    throw new Exception("User not found.");
                }

                return new
                {
                    errorCode = 200,
                    invoice = invoice,
                    details = details,
                    user = new
                    {
                        user.CompanyName,
                        user.CompanyNameAr,
                        user.EstablishmentName,
                        user.EstablishmentNameAr,
                        user.Country,
                        user.CountryAr,
                        user.City,
                        user.CityAr,
                        user.Vatnumber,
                        user.AccountNo,
                        user.AccountTitle,
                        user.BankName,
                        user.Branch,
                        user.SwiftCode,
                        user.Iban
                    }
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    errorCode = 500,
                    errorMessage =
                        ex.InnerException?.Message ?? ex.Message
                };
            }
        }

        public async Task<dynamic> RegenerateInvoices(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var from = fromDate?.Date;
                var toExclusive = toDate?.Date.AddDays(1);

                var invoices = await _context.Set<PurchaseInvoice>()
                    .AsNoTracking()
                    .Where(x => x.IsCancelled == null || x.IsCancelled == false)
                    .Where(x => !from.HasValue || x.InvoiceDate == null || x.InvoiceDate >= from.Value)
                    .Where(x => !toExclusive.HasValue || x.InvoiceDate == null || x.InvoiceDate < toExclusive.Value)
                    .OrderBy(x => x.InvoiceId)
                    .Select(x => new { x.InvoiceId, x.InvoiceNo, x.ReferenceId, x.ServiceProviderId })
                    .ToListAsync();

                int successCount = 0;
                var failures = new List<string>();

                foreach (var row in invoices)
                {
                    try
                    {
                        int? referenceId = row.ReferenceId ?? _session.ReferenceId;

                        var invoiceData = await GetInvoiceById(row.InvoiceId, row.ServiceProviderId ?? 0, referenceId);
                        dynamic data = invoiceData;

                        if (data.errorCode != 200)
                        {
                            failures.Add($"{row.InvoiceNo}: {data.errorMessage}");
                            continue;
                        }

                        var user = await _context.Users
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x => x.ReferenceId == referenceId);

                        if (user == null)
                        {
                            failures.Add($"{row.InvoiceNo}: Company user not found");
                            continue;
                        }

                        await RegenerateSinglePurchaseInvoicePdf(data, user, row.InvoiceNo);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logs.Write("PurchaseInvoice", "RegenerateInvoices", $"{row.InvoiceNo} - {ex}");
                        failures.Add($"{row.InvoiceNo}: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }

                return new
                {
                    errorCode = 200,
                    message = "Purchase invoice regeneration completed",
                    total = invoices.Count,
                    successCount,
                    failureCount = failures.Count,
                    failures
                };
            }
            catch (Exception ex)
            {
                _logs.Write("PurchaseInvoice", "RegenerateInvoices", ex.ToString());
                return new
                {
                    errorCode = 500,
                    errorMessage = ex.InnerException?.Message ?? ex.Message
                };
            }
        }

        private async Task RegenerateSinglePurchaseInvoicePdf(dynamic invoiceData, User user, string invoiceNo)
        {
            dynamic data = invoiceData;
            var inv = data.invoice;
            var details = data.details;

            if (inv == null) throw new Exception("Invoice data not found");

            var basePath = Directory.GetCurrentDirectory();
            var filePath = Path.Combine(basePath, "wwwroot", "templates", "pdf", "purchaseinvoicenew.html");

            var fullHtml = File.Exists(filePath)
                ? await File.ReadAllTextAsync(filePath)
                : "<html><body>Purchase Invoice</body></html>";

            var invoiceRows = new StringBuilder();
            int sr = 1;

            foreach (var item in details)
            {
                invoiceRows.AppendFormat(
                    "<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td><td>{6}</td><td>{7}</td></tr>",
                    sr++,
                    item.InvoiceDetail,
                    item.Unit,
                    item.Qty,
                    item.Rate,
                    item.Amount,
                    item.VatAmount,
                    item.TotalAmount
                );
            }

            fullHtml = fullHtml
                .Replace("{{CompanyName}}", user.CompanyName ?? "")
                .Replace("{{CompanyNameAr}}", user.CompanyNameAr ?? "")
                .Replace("{{EstablishmentName}}", user.EstablishmentName ?? "")
                .Replace("{{EstablishmentNameAr}}", user.EstablishmentNameAr ?? "")
                .Replace("{{Country}}", user.Country ?? "")
                .Replace("{{CountryAr}}", user.CountryAr ?? "")
                .Replace("{{City}}", user.City ?? "")
                .Replace("{{CityAr}}", user.CityAr ?? "")
                .Replace("{{VATNumber}}", user.Vatnumber ?? "")
                .Replace("{{InvoiceNo}}", inv.InvoiceNo ?? "")
                .Replace("{{InvoiceDate}}", Convert.ToDateTime(inv.InvoiceDate).ToString("dd-MMM-yyyy"))
                .Replace("{{InvoiceStatus}}", inv.StatusName ?? "")
                .Replace("{{CustomerName}}", inv.CustomerName ?? "")
                .Replace("{{CustomerAddress}}", inv.CustomerAddress ?? "")
                .Replace("{{CustomerCity}}", $"{inv.RegionName} {inv.CountryName}")
                .Replace("{{CustomerVAT}}", inv.CustomerVat ?? "")
                .Replace("{{JobNumber}}", inv.JobNumber ?? "")
                .Replace("{{ModeOfShipment}}", inv.ModeOfShipment ?? "")
                .Replace("{{ShipmentType}}", inv.ShipmentType ?? "")
                .Replace("{{POL}}", inv.POL ?? "")
                .Replace("{{POD}}", inv.POD ?? "")
                .Replace("{{BLNumber}}", inv.BLNumber ?? "")
                .Replace("{{PaymentTerms}}", "")
                .Replace("{{InvoiceRows}}", invoiceRows.ToString())
                .Replace("{{Total}}", inv.TotalAmount?.ToString("N2") ?? "0")
                .Replace("{{Advance}}", inv.Advance?.ToString("N2") ?? "0")
                .Replace("{{Discount}}", inv.Discount?.ToString("N2") ?? "0")
                .Replace("{{NetAmount}}", inv.NetAmount?.ToString("N2") ?? "0")
                .Replace("{{VAT}}", inv.VatAmount?.ToString("N2") ?? "0")
                .Replace("{{GrandTotal}}", inv.GrandTotal?.ToString("N2") ?? "0")
                .Replace("{{AmountInWords}}", inv.AmountInWords ?? "")
                .Replace("{{AccountNo}}", user.AccountNo ?? "")
                .Replace("{{AccountTitle}}", user.AccountTitle ?? "")
                .Replace("{{BankName}}", user.BankName ?? "")
                .Replace("{{Branch}}", user.Branch ?? "")
                .Replace("{{SwiftCode}}", user.SwiftCode ?? "")
                .Replace("{{Iban}}", user.Iban ?? "")
                .Replace("{{PrintDate}}", DateTime.Now.ToString("dd-MMM-yyyy hh:mm tt"))
                .Replace("{{QRCodeValue}}", await GetQrCodeHtml(user, inv));

            var pdfBytes = GeneratePdfFromHtml(fullHtml);
            var pdfFolder = Path.Combine(basePath, "wwwroot", "pdfs", "purchase");

            if (!Directory.Exists(pdfFolder)) Directory.CreateDirectory(pdfFolder);

            var pdfPath = Path.Combine(pdfFolder, $"{invoiceNo}.pdf");
            await File.WriteAllBytesAsync(pdfPath, pdfBytes);
        }

        public async Task<dynamic> EditPurchaseInvoice(int invoiceId)
        {
            try
            {
                SqlParameter[] parameter =
                {
                    new SqlParameter { ParameterName = "@InvoiceId", Value = invoiceId },
                };

                var result = await _db.Fetch(
                    "sp_EditPurchaseInvoice",
                    parameter,
                    CommandType.StoredProcedure
                );

                ErrorResponse errorResponse = result.error;

                if (!errorResponse.Error)
                {
                    if (result.tables.Count > 0 && DataHelper.HasRows(result.tables[0]))
                    {
                        return new
                        {
                            errorCode = 200,
                            data = new
                            {
                                JobImportMaster = result.tables.Count > 0
                                    ? result.tables[0]
                                    : null,

                                JobImportPayment = result.tables.Count > 1
                                    ? result.tables[1]
                                    : null
                            }
                        };
                    }

                    return new
                    {
                        errorCode = 404,
                        errorMessage = "No Record Found"
                    };
                }

                return new
                {
                    errorCode = 999,
                    errorMessage = errorResponse.ErrorList[0].Message
                };
            }
            catch (Exception ex)
            {
                _logs.Write(
                    "Purchase Invoice",
                    "EditPurchaseInvoice",
                    ex.InnerException?.Message ?? ex.Message
                );

                return new
                {
                    errorCode = 999,
                    errorMessage = ex.Message
                };
            }
        }

        public byte[] GeneratePdfFromHtml(string html)
        {
            var doc = new HtmlToPdfDocument
            {
                GlobalSettings = {
                PaperSize = PaperKind.A4,
                Orientation = Orientation.Portrait
            },
                Objects = {
                new ObjectSettings
                {
                    HtmlContent = html,
                    WebSettings =
                    {
                        DefaultEncoding = "utf-8",
                        LoadImages = true,
                        PrintMediaType = true,
                        EnableIntelligentShrinking = true
                    }
                }
            }
            };

            return _converter.Convert(doc);
        }

        public async Task<dynamic> SavePurchaseVoucher(PurchaseVoucher model)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    var invoice = await _context.Set<PurchaseInvoice>()
                        .FirstOrDefaultAsync(x => x.InvoiceId == model.InvoiceId);

                    if (invoice == null)
                    {
                        return new
                        {
                            errorCode = 404,
                            data = "Invoice not found"
                        };
                    }

                    decimal totalReceived = await _context.PurchaseVouchers
                        .Where(x => x.InvoiceId == model.InvoiceId)
                        .SumAsync(x => (decimal?)x.Amount) ?? 0;

                    if (model.Id > 0)
                    {
                        var existingPayment = await _context.PurchaseVouchers
                            .FirstOrDefaultAsync(x => x.Id == model.Id);

                        if (existingPayment != null)
                        {
                            totalReceived -= existingPayment.Amount;
                        }
                    }

                    if ((totalReceived + model.Amount) > invoice.GrandTotal)
                    {
                        return new
                        {
                            errorCode = 201,
                            data = $"Payment exceeds invoice amount. Remaining balance is {(invoice.GrandTotal - totalReceived):N2}"
                        };
                    }

                    PurchaseVoucher payment;
                    IDictionary<string, object?>? auditVoucherBefore = null;
                    decimal auditPaidBefore = invoice.PaidAmount ?? 0;
                    decimal auditBalanceBefore = invoice.BalanceAmount ?? 0;
                    int auditStatusBefore = invoice.Status;

                    // ========== AUDIT: capture voucher state BEFORE any change ==========
                    if (model.Id > 0)
                    {
                        var auditExisting = await _context.PurchaseVouchers
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x => x.Id == model.Id);

                        if (auditExisting != null)
                        {
                            auditVoucherBefore = _audit.Snapshot(auditExisting);
                        }
                    }

                    if (model.Id == 0)
                    {
                        var maxId = await _context.PurchaseVouchers
                            .MaxAsync(x => (int?)x.Id) ?? 0;

                        payment = new PurchaseVoucher
                        {
                            Id = maxId + 1,
                            InvoiceId = model.InvoiceId,
                            Pvnumber = model.Pvnumber,
                            PaymentDate = model.PaymentDate,
                            Amount = model.Amount,
                            PaymentMode = model.PaymentMode,
                            BlNo = model.BlNo,
                            Description = model.Description,
                            IsActive = true,
                            CreatedBy = _session.LoginId,
                            CreatedOn = DateTime.Now
                        };

                        _context.PurchaseVouchers.Add(payment);
                    }
                    else
                    {
                        payment = await _context.PurchaseVouchers
                            .FirstOrDefaultAsync(x => x.Id == model.Id);

                        if (payment == null)
                        {
                            return new
                            {
                                errorCode = 404,
                                data = "Payment record not found"
                            };
                        }

                        payment.Pvnumber = model.Pvnumber;
                        payment.PaymentDate = model.PaymentDate;
                        payment.Amount = model.Amount;
                        payment.PaymentMode = model.PaymentMode;
                        payment.BlNo = model.BlNo;
                        payment.Description = model.Description;
                        payment.ModifiedBy = _session.LoginId;
                        payment.ModifiedOn = DateTime.Now;
                    }

                    await _context.SaveChangesAsync();

                    totalReceived = await _context.PurchaseVouchers
                        .Where(x => x.InvoiceId == model.InvoiceId)
                        .SumAsync(x => (decimal?)x.Amount) ?? 0;

                    invoice.PaidAmount = totalReceived;
                    invoice.BalanceAmount = invoice.GrandTotal - totalReceived;

                    if (totalReceived <= 0)
                    {
                        invoice.Status = 0;
                    }
                    else if (totalReceived < invoice.GrandTotal)
                    {
                        invoice.Status = 2;
                    }
                    else
                    {
                        invoice.Status = 1;
                    }

                    await _context.SaveChangesAsync();

                    transaction.Commit();

                    // ========== AUDIT: record voucher create / update ==========
                    await _audit.RecordAsync(new AuditEntryRequest
                    {
                        Module = "Purchase Voucher",
                        Action = model.Id > 0 ? "Update" : "Create",
                        EntityName = "PurchaseVoucher",
                        EntityId = payment.Pvnumber ?? $"Voucher#{payment.Id}",
                        Description = $"PV {payment.Pvnumber} of {model.Amount:N2} recorded against purchase invoice {invoice.InvoiceNo} " +
                                      $"(mode: {model.PaymentMode}). Invoice paid {auditPaidBefore:N2} -> {invoice.PaidAmount:N2}, " +
                                      $"balance {auditBalanceBefore:N2} -> {invoice.BalanceAmount:N2}, " +
                                      $"status {StatusHelper.GetInvoiceStatus(auditStatusBefore)} -> {StatusHelper.GetInvoiceStatus(invoice.Status)}.",
                        OldValues = auditVoucherBefore,
                        NewValues = _audit.Snapshot(payment),
                        PageName = "/Invoice/PurchaseInvoice"
                    });

                    // Regenerate PDF so the updated status/paid/balance is reflected
                    try
                    {
                        var invoiceData = await GetInvoiceById(invoice.InvoiceId, invoice.ServiceProviderId ?? 0);
                        dynamic data = invoiceData;

                        if (data.errorCode == 200)
                        {
                            var user = await _context.Users
                                .AsNoTracking()
                                .FirstOrDefaultAsync(x => x.ReferenceId == _session.ReferenceId);

                            if (user != null)
                            {
                                await RegenerateSinglePurchaseInvoicePdf(data, user, invoice.InvoiceNo!);
                            }
                        }
                    }
                    catch (Exception pdfEx)
                    {
                        _logs.Write("PurchaseInvoice", "SavePurchaseVoucher_PdfRegenerate", pdfEx.ToString());
                    }

                    return new
                    {
                        errorCode = 200,
                        data = "Payment saved successfully",
                        paidAmount = invoice.PaidAmount,
                        balanceAmount = invoice.BalanceAmount,
                        status = invoice.Status
                    };
                }
                catch (Exception ex)
                {
                    transaction.Rollback();

                    _logs.Write(
                        "PurchaseInvoice",
                        "SavePurchaseVoucher",
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

        public async Task<dynamic> GetPaymentHistory(int invoiceId)
        {
            try
            {
                var invoice = await _context.Set<PurchaseInvoice>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId);

                if (invoice == null)
                {
                    return new { errorCode = 404, data = "Invoice not found" };
                }

                var payments = await (from p in _context.PurchaseVouchers
                                      join u in _context.Users on p.CreatedBy equals u.Id into pu
                                      from user in pu.DefaultIfEmpty()
                                      where p.InvoiceId == invoiceId && p.IsActive
                                      orderby p.PaymentDate ascending, p.Id ascending
                                      select new
                                      {
                                          p.Id,
                                          p.Pvnumber,
                                          p.PaymentDate,
                                          p.Amount,
                                          p.PaymentMode,
                                          p.BlNo,
                                          p.Description,
                                          p.CreatedOn,
                                          CreatedByName = user != null ? user.UserName : null
                                      }).ToListAsync();

                return new
                {
                    errorCode = 200,
                    invoiceNo = invoice.InvoiceNo,
                    paidAmount = invoice.PaidAmount,
                    balanceAmount = invoice.BalanceAmount,
                    totalReceived = payments.Count,
                    data = payments
                };
            }
            catch (Exception ex)
            {
                _logs.Write("PurchaseInvoice", "GetPaymentHistory", ex.InnerException?.Message ?? ex.Message);

                return new { errorCode = 999, data = ex.Message };
            }
        }

        public async Task<dynamic> GetSalesReport(IList<QueryFilters> filters)
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
                new SqlParameter("@ReferenceId", _session.ReferenceId > 0 ? (object)_session.ReferenceId : DBNull.Value),
                new SqlParameter("@CostCenterId", string.IsNullOrEmpty(costCenterId) ? DBNull.Value : costCenterId),
                new SqlParameter("@FromDate", string.IsNullOrEmpty(fromDate) ? DBNull.Value : fromDate),
                new SqlParameter("@ToDate", string.IsNullOrEmpty(toDate) ? DBNull.Value : toDate),
                new SqlParameter("@Status", string.IsNullOrEmpty(status) ? DBNull.Value : status),
                new SqlParameter("@OrderColumn", orderColumn ?? "Id"),
                new SqlParameter("@OrderDir", orderDir ?? "desc")
            };

            var result = await _db.Fetch(
                "sp_GetPurchaseReport",
                parameters,
                CommandType.StoredProcedure
            );

            var table = result.tables[0];

            int totalRecords = table.Rows.Count;

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

        public async Task<dynamic> GetServiceProviderStatement(IList<QueryFilters> filters)
        {
            int draw = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "draw")?.filterValue ?? "0");
            int start = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "start")?.filterValue ?? "0");
            int length = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "length")?.filterValue ?? "10");

            string serviceProviderId = filters.FirstOrDefault(x => x.fieldName == "serviceProviderId")?.filterValue;
            string fromDate = filters.FirstOrDefault(x => x.fieldName == "fromDate")?.filterValue;
            string toDate = filters.FirstOrDefault(x => x.fieldName == "toDate")?.filterValue;

            SqlParameter[] parameters =
            {
                new SqlParameter("@ServiceProviderId", string.IsNullOrEmpty(serviceProviderId) ? DBNull.Value : serviceProviderId),
                new SqlParameter("@FromDate", string.IsNullOrEmpty(fromDate) ? DBNull.Value : fromDate),
                new SqlParameter("@ToDate", string.IsNullOrEmpty(toDate) ? DBNull.Value : toDate),
            };

            var result = await _db.Fetch(
                "sp_GetServiceProviderPurchaseSOA",
                parameters,
                CommandType.StoredProcedure
            );

            var table = result.tables[0];

            int totalRecords = table.Rows.Count;

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

        public async Task<dynamic> GetAgingReport(IList<QueryFilters> filters)
        {
            int draw = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "draw")?.filterValue ?? "0");
            int start = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "start")?.filterValue ?? "0");
            int length = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "length")?.filterValue ?? "10");

            int partyId = 0;
            int.TryParse(filters.FirstOrDefault(x => x.fieldName == "partyId")?.filterValue, out partyId);

            int costCenterId = 0;
            int.TryParse(filters.FirstOrDefault(x => x.fieldName == "costCenterId")?.filterValue, out costCenterId);

            DateTime asOfDate = DateTime.Today;
            if (DateTime.TryParse(filters.FirstOrDefault(x => x.fieldName == "asOfDate")?.filterValue, out DateTime ad))
            {
                asOfDate = ad.Date;
            }

            var rows = await (
                from p in _context.PurchaseInvoices
                join sp in _context.Customers on p.ServiceProviderId equals sp.Id into spJoin
                from sp in spJoin.DefaultIfEmpty()
                join j in _context.JobImportMasters on p.JobId equals j.Id into jobJoin
                from j in jobJoin.DefaultIfEmpty()
                where p.ReferenceId == _session.ReferenceId
                    && p.IsCancelled == false
                    && (p.BalanceAmount ?? 0) > 0
                select new
                {
                    p.InvoiceId,
                    p.InvoiceNo,
                    p.InvoiceDate,
                    PartyId = p.ServiceProviderId,
                    PartyName = sp != null ? sp.CustomerName : p.ServiceProviderName,
                    JobNo = j != null ? j.JobNumber : null,
                    BlNo = j != null ? j.BlNo : null,
                    CostCenterId = j != null ? (int?)j.CostCenterId : null,
                    p.GrandTotal,
                    p.PaidAmount,
                    p.BalanceAmount,
                    p.Status
                }).ToListAsync();

            rows = rows
                .Where(x => (partyId == 0 || x.PartyId == partyId))
                .Where(x => (costCenterId == 0 || x.CostCenterId == costCenterId))
                .ToList();

            var data = rows.Select(r =>
            {
                int age = (asOfDate - r.InvoiceDate.Date).Days;
                if (age < 0) age = 0;

                decimal bal = (decimal)(r.BalanceAmount ?? 0m);

                return new Dictionary<string, object>
                {
                    ["InvoiceNo"] = r.InvoiceNo,
                    ["InvoiceDate"] = r.InvoiceDate,
                    ["PartyName"] = r.PartyName,
                    ["JobNo"] = r.JobNo,
                    ["BlNo"] = r.BlNo,
                    ["AgeDays"] = age,
                    ["Current"] = age <= 30 ? bal : 0m,
                    ["Days31_60"] = age > 30 && age <= 60 ? bal : 0m,
                    ["Days61_90"] = age > 60 && age <= 90 ? bal : 0m,
                    ["Days90Plus"] = age > 90 ? bal : 0m,
                    ["Total"] = bal,
                    ["Status"] = StatusHelper.GetInvoiceStatus(r.Status)
                };
            }).ToList();

            var ordered = data.OrderByDescending(x => x["InvoiceNo"]).ToList();

            int totalRecords = data.Count;

            return new
            {
                draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecords,
                data = ordered.Skip(start).Take(length).ToList(),
                errorCode = 200
            };
        }

        public async Task<bool> IsInvoiceEditable(int invoiceId)
        {
            return !await _context.PurchaseInvoices
                .AsNoTracking()
                .AnyAsync(x => x.InvoiceId == invoiceId && (x.PaidAmount ?? 0) > 0);
        }

        public async Task<dynamic> GetNextCreditNoteNumber()
        {
            var year = DateTime.Now.Year;

            string prefix = $"PCN-{year}-";

            var lastCreditNoteNo = await _context.PurchaseCreditNotes
                .Where(x => x.CreditNoteNo.StartsWith(prefix))
                .OrderByDescending(x => x.CreditNoteNo)
                .Select(x => x.CreditNoteNo)
                .FirstOrDefaultAsync();

            int nextNumber = 1;

            if (!string.IsNullOrWhiteSpace(lastCreditNoteNo))
            {
                var numberPart = lastCreditNoteNo.Substring(prefix.Length);

                if (int.TryParse(numberPart, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            return $"{prefix}{nextNumber:D4}";
        }

        public async Task<dynamic> CancelInvoice(int invoiceId)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    var invoice = await _context.PurchaseInvoices
                        .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId);

                    if (invoice == null)
                    {
                        return new
                        {
                            errorCode = 404,
                            data = "Invoice not found."
                        };
                    }

                    if (invoice.IsCancelled == true)
                    {
                        return new
                        {
                            errorCode = 201,
                            data = "Invoice is already cancelled."
                        };
                    }

                    string creditNoteNo = await GetNextCreditNoteNumber();

                    var maxId = await _context.PurchaseCreditNotes
                        .MaxAsync(x => (int?)x.CreditNoteId) ?? 0;

                    PurchaseCreditNote creditNote = new PurchaseCreditNote
                    {
                        CreditNoteId = maxId + 1,
                        CreditNoteNo = creditNoteNo,
                        InvoiceId = invoice.InvoiceId,
                        InvoiceNo = invoice.InvoiceNo,
                        InvoiceDate = invoice.InvoiceDate,
                        ServiceProviderId = (int)invoice.ServiceProviderId,
                        NetAmount = invoice.NetAmount,
                        VatAmount = invoice.VatAmount,
                        GrandTotal = invoice.GrandTotal,
                        CreditNoteDate = DateTime.Now,
                        IsActive = true,
                        ReferenceId = _session.ReferenceId,
                        CreatedBy = _session.LoginId,
                        CreatedOn = DateTime.Now
                    };

                    _context.PurchaseCreditNotes.Add(creditNote);

                    // ========== AUDIT: capture invoice state BEFORE cancelling ==========
                    var auditInvoiceBefore = _audit.Snapshot(invoice);

                    invoice.IsCancelled = true;
                    invoice.CancelledBy = _session.LoginId;
                    invoice.CancelledOn = DateTime.Now;
                    invoice.ModifiedBy = _session.LoginId;
                    invoice.ModifiedOn = DateTime.Now;

                    await _context.SaveChangesAsync();

                    transaction.Commit();

                    // ========== AUDIT: record cancellation ==========
                    await _audit.RecordAsync(new AuditEntryRequest
                    {
                        Module = "Purchase Invoice",
                        Action = "Cancel",
                        EntityName = "PurchaseInvoice",
                        EntityId = invoice.InvoiceNo,
                        Description = $"Purchase invoice {invoice.InvoiceNo} cancelled. Credit note {creditNoteNo} generated for {invoice.GrandTotal:N2}.",
                        OldValues = auditInvoiceBefore,
                        NewValues = _audit.Snapshot(invoice),
                        PageName = "/Invoice/PurchaseInvoice"
                    });

                    return new
                    {
                        errorCode = 200,
                        data = "Invoice cancelled successfully.",
                        creditNoteNo = creditNoteNo
                    };
                }
                catch (Exception ex)
                {
                    transaction.Rollback();

                    _logs.Write(
                        "PurchaseInvoice",
                        "CancelPurchaseInvoice",
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

        public async Task<dynamic> GetCreditNoteSummary(int invoiceId)
        {
            try
            {
                var data = await (from cn in _context.PurchaseCreditNotes

                                  join inv in _context.PurchaseInvoices
                                      on cn.InvoiceId equals inv.InvoiceId

                                  join sp in _context.Customers
                                      on inv.ServiceProviderId equals sp.Id

                                  where cn.InvoiceId == invoiceId

                                  select new
                                  {
                                      cn.CreditNoteId,
                                      cn.CreditNoteNo,
                                      cn.InvoiceNo,
                                      ServiceProviderName = sp.CustomerName,
                                      cn.CreditNoteDate,
                                      cn.NetAmount,
                                      cn.VatAmount,
                                      cn.GrandTotal,
                                      cn.Remarks,
                                      inv.CancelledOn,
                                      inv.CancelledBy
                                  })
                                  .FirstOrDefaultAsync();

                if (data == null)
                {
                    return new
                    {
                        errorCode = 404,
                        data = "Credit Note not found."
                    };
                }

                string cancelledByUser = "";

                if (data.CancelledBy.HasValue)
                {
                    cancelledByUser = await _context.Users
                        .Where(x => x.Id == data.CancelledBy.Value)
                        .Select(x => x.UserName)
                        .FirstOrDefaultAsync() ?? "";
                }

                return new
                {
                    errorCode = 200,
                    data = new
                    {
                        data.CreditNoteId,
                        data.CreditNoteNo,
                        data.InvoiceNo,
                        data.ServiceProviderName,
                        data.CreditNoteDate,
                        data.NetAmount,
                        data.VatAmount,
                        data.GrandTotal,
                        data.Remarks,
                        data.CancelledOn,
                        CancelledByUser = cancelledByUser
                    }
                };
            }
            catch (Exception ex)
            {
                _logs.Write(
                    "PurchaseInvoice",
                    "GetPurchaseCreditNoteSummary",
                    ex.InnerException?.Message ?? ex.Message
                );

                return new
                {
                    errorCode = 999,
                    data = ex.Message
                };
            }
        }

        public async Task<long> GetNextPurchaseInvoiceSequenceNumber()
        {
            // Existing project pattern: Database.OpenConnectionAsync / CloseConnectionAsync
            await _context.Database.OpenConnectionAsync();
            try
            {
                using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT NEXT VALUE FOR dbo.PurchaseInvoiceSequence";

                var currentTransaction = _context.Database.CurrentTransaction;
                if (currentTransaction != null)
                {
                    command.Transaction = currentTransaction.GetDbTransaction();
                }

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt64(result);
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }

        public async Task<dynamic> GetNextPurchaseInvoiceNumber()
        {
            var currentYear = DateTime.Now.Year;
            var prefix = $"PINV-{currentYear}-";

            // 1. Current year ke tamam valid purchase invoice numbers memory me fetch karein
            var invoiceNumbers = await _context.PurchaseInvoices
                .Where(x => x.InvoiceNo != null && x.InvoiceNo.StartsWith(prefix))
                .Select(x => x.InvoiceNo)
                .ToListAsync();

            int maxNumber = 0;

            // 2. Har invoice number ka last part parse karke Max find karein
            foreach (var invNo in invoiceNumbers)
            {
                var parts = invNo.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int num))
                {
                    if (num > maxNumber)
                    {
                        maxNumber = num;
                    }
                }
            }

            // 3. Highest number me +1 add karein
            int nextNumber = maxNumber + 1;

            var invoiceNo = $"PINV-{currentYear}-{nextNumber:D4}";

            return new
            {
                errorCode = 200,
                invoiceNo
            };
        }

        private async Task UpdatePurchaseSequenceIfHigherAsync(int customSeqNum)
        {
            string sequenceName = "PurchaseInvoiceSequence";

            string query = $@"
                DECLARE @CurrentSeq BIGINT;
                SELECT @CurrentSeq = CAST(current_value AS BIGINT) 
                FROM sys.sequences 
                WHERE name = '{sequenceName}';

                IF ({customSeqNum} >= @CurrentSeq)
                BEGIN
                    DECLARE @NextValue BIGINT = {customSeqNum} + 1;
                    DECLARE @Sql NVARCHAR(MAX) = 'ALTER SEQUENCE dbo.{sequenceName} RESTART WITH ' + CAST(@NextValue AS NVARCHAR(20));
                    EXEC sp_executesql @Sql;
                END";

            await _context.Database.ExecuteSqlRawAsync(query);
        }
    }
}