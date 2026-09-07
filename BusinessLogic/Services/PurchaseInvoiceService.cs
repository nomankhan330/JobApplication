using BusinessLogic.Helper;
using BusinessLogic.Interfaces;
using BusinessLogic.Models;
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

        public void Dispose()
        {
            // no-op
        }

        public PurchaseInvoiceService(AppDbContext context, ISessionHelper session, ILogs logs, IDatabaseObject db, IConverter converter)
        {
            _context = context;
            _session = session;
            _logs = logs;
            _db = db;
            _converter = converter;
        }

        public async Task<dynamic> Save(PurchaseInvoiceVM model, int loginId)
        {
            bool isEdit = model.InvoiceId > 0;
            PurchaseInvoice invoice = null!;

            // Regex pattern validation for custom creation format
            var invoiceNoRegex = new Regex(@"^PINV-\d{4}-\d{4}$", RegexOptions.IgnoreCase);

            // ==========================================
            // DATABASE TRANSACTION
            // ==========================================
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Check Duplicate: Same Job ID + Same Service Provider/Vendor (Exclude current record if Editing)
                var alreadyExists = await _context.Set<PurchaseInvoice>()
                    .AnyAsync(x => x.JobId == model.JobId
                                && x.ServiceProviderId == model.ServiceProviderId
                                && (!isEdit || x.InvoiceId != model.InvoiceId));

                if (alreadyExists)
                {
                    await transaction.RollbackAsync();
                    return new
                    {
                        errorCode = 409,
                        errorMessage = "An invoice for this Service Provider already exists against the selected Job."
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
                        await transaction.RollbackAsync();
                        return new { errorCode = 404, errorMessage = "Purchase invoice not found for update." };
                    }

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
                var filePath = Path.Combine(basePath, "wwwroot", "templates", "pdf", "purchaseinvoice.html");

                var fullHtml = File.Exists(filePath)
                    ? await File.ReadAllTextAsync(filePath)
                    : "<html><body>Purchase Invoice</body></html>";

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
                    .Replace("{{QRCodeValue}}", "");

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
                    .Replace("{{QRCodeValue}}", "");

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
                fullHtml = fullHtml.Replace("{{QRCodeValue}}", "");

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
                        StatusName = StatusHelper.GetInvoiceStatus(0),
                        CreatedByUser = u.UserName
                    };

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

        public async Task<dynamic> GetInvoiceById(int invoiceId, int serviceProviderId)
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

                return new
                {
                    errorCode = 200,
                    invoice = invoice,
                    details = details
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

                    invoice.IsCancelled = true;
                    invoice.CancelledBy = _session.LoginId;
                    invoice.CancelledOn = DateTime.Now;
                    invoice.ModifiedBy = _session.LoginId;
                    invoice.ModifiedOn = DateTime.Now;

                    await _context.SaveChangesAsync();

                    transaction.Commit();

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