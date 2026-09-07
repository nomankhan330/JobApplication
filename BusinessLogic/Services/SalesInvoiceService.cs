using Azure.Core;
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
using System.Drawing;
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
    public class SalesInvoiceService : ISalesInvoice
    {
        private readonly AppDbContext _context;
        private readonly ISessionHelper _session;
        private readonly ILogs _logs;
        private readonly IDatabaseObject _db;
        private readonly IConverter _converter;

        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public SalesInvoiceService(AppDbContext context, ISessionHelper session, ILogs logs, IDatabaseObject db, IConverter converter)
        {
            _context = context;
            _session = session;
            _logs = logs;
            _db = db;
            _converter = converter;
        }

        public async Task<dynamic> Save(SalesInvoiceVM model, int loginId)
        {
            bool isEdit = model.InvoiceId > 0;
            SalesInvoice invoice = null!;

            // Regex pattern validation for custom creation
            var invoiceNoRegex = new Regex(@"^INV-\d{4}-\d{4}$", RegexOptions.IgnoreCase);

            // ==========================================
            // DATABASE TRANSACTION
            // ==========================================
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Check Duplicate: Same Job ID + Same Customer (Exclude current record if Editing)
                var alreadyExists = await _context.Set<SalesInvoice>()
                    .AnyAsync(x => x.JobId == model.JobId
                                && x.CustomerId == model.CustomerId
                                && (!isEdit || x.InvoiceId != model.InvoiceId));

                if (alreadyExists)
                {
                    await transaction.RollbackAsync();
                    return new
                    {
                        errorCode = 409,
                        errorMessage = "An invoice for this Customer already exists against the selected Job."
                    };
                }

                if (isEdit)
                {
                    // ==========================================
                    // EDIT / UPDATE LOGIC
                    // InvoiceNo ko touch ya check nahi karenge
                    // DB se existing invoice fetch hoga
                    // ==========================================
                    invoice = await _context.SalesInvoices
                        .FirstOrDefaultAsync(x => x.InvoiceId == model.InvoiceId);

                    if (invoice == null)
                    {
                        await transaction.RollbackAsync();
                        return new { errorCode = 404, errorMessage = "Invoice not found for update." };
                    }

                    // UPDATE ONLY HEADER DATA (InvoiceNo strictly retain hoga)
                    invoice.CustomerId = model.CustomerId;
                    invoice.JobId = model.JobId;
                    invoice.TotalAmount = model.TotalAmount;
                    invoice.Advance = model.Advance;
                    invoice.Discount = model.Discount;
                    invoice.NetAmount = model.NetAmount;
                    invoice.VatAmount = model.VatAmount;
                    invoice.GrandTotal = model.GrandTotal;
                    invoice.ModifiedBy = loginId;
                    invoice.ModifiedOn = DateTime.Now;

                    _context.SalesInvoices.Update(invoice);

                    // DELETE OLD DETAILS
                    var existingDetails = await _context.SalesInvoiceDetails
                        .Where(x => x.InvoiceId == model.InvoiceId)
                        .ToListAsync();

                    if (existingDetails.Count > 0)
                    {
                        _context.SalesInvoiceDetails.RemoveRange(existingDetails);
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
                                errorMessage = "Invalid invoice number format. Format must be INV-2026-0005."
                            };
                        }

                        finalInvoiceNo = model.InvoiceNo;

                        // DUPLICATE CHECK
                        bool invoiceExists = await _context.SalesInvoices
                            .AnyAsync(x => x.InvoiceNo == finalInvoiceNo);

                        if (invoiceExists)
                        {
                            await transaction.RollbackAsync();
                            return new { errorCode = 409, errorMessage = "This invoice number already exists." };
                        }

                        // EXTRACT SEQUENCE NUMBER & RE-SEED IF HIGHER
                        var parts = finalInvoiceNo.Split('-');
                        if (parts.Length == 3 && int.TryParse(parts[2], out int customSeqNum))
                        {
                            await UpdateSequenceIfHigherAsync(customSeqNum);
                        }
                    }
                    else
                    {
                        // NO INVOICE NUMBER PROVIDED -> GENERATE FROM SQL SEQUENCE
                        var sequenceNumber = await GetNextSalesInvoiceSequenceNumber();
                        finalInvoiceNo = $"INV-{DateTime.Now.Year}-{sequenceNumber:D4}";
                    }

                    // CREATE INVOICE
                    invoice = new SalesInvoice
                    {
                        InvoiceNo = finalInvoiceNo,
                        CustomerId = model.CustomerId,
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

                    _context.SalesInvoices.Add(invoice);
                }

                // SAVE INVOICE HEADER
                await _context.SaveChangesAsync();

                // SAVE INVOICE DETAILS
                if (model.Items != null && model.Items.Count > 0)
                {
                    var detailsList = model.Items.Select(item => new SalesInvoiceDetail
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

                    await _context.SalesInvoiceDetails.AddRangeAsync(detailsList);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();

                if (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
                {
                    return new { errorCode = 409, errorMessage = "This invoice number already exists." };
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
                var invoiceData = await GetInvoiceById(invoice.InvoiceId);
                if (invoiceData == null) throw new Exception("Invoice data not found for PDF generation");

                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ReferenceId == _session.ReferenceId);

                if (user == null) throw new Exception("User not found.");

                dynamic data = invoiceData;
                var inv = data.invoice;
                var details = data.details;

                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, "wwwroot", "templates", "pdf", "salesinvoice.html");
                var fullHtml = await File.ReadAllTextAsync(filePath);

                var invoiceRows = new StringBuilder();
                int sr = 1;

                foreach (var item in details)
                {
                    invoiceRows.AppendFormat(
                        "<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td><td>{6}</td><td>{7}</td><td>{8}</td></tr>",
                        sr++, item.PaymentType, item.InvoiceDetail, item.Unit, item.Qty, item.Rate, item.Amount, item.VatAmount, item.TotalAmount
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
                    .Replace("{{InvoiceStatus}}", StatusHelper.GetInvoiceStatus(inv.Status) ?? "")
                    .Replace("{{CustomerName}}", inv.CustomerName ?? "")
                    .Replace("{{CustomerNameAr}}", inv.CustomerNameAr ?? "")
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
                var pdfFolder = Path.Combine(basePath, "wwwroot", "pdfs", "invoice");

                if (!Directory.Exists(pdfFolder)) Directory.CreateDirectory(pdfFolder);

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
                _logs.Write("SalesInvoice", "Save", ex.ToString());
                return new
                {
                    errorCode = 500,
                    errorMessage = ex.InnerException?.Message ?? ex.Message
                };
            }
        }

        public async Task<dynamic> Save_Bk6(SalesInvoiceVM model, int loginId)
        {
            bool isEdit = model.InvoiceId > 0;
            SalesInvoice invoice = null!;

            // Regex pattern for validation
            var invoiceNoRegex = new Regex(@"^INV-\d{4}-\d{4}$", RegexOptions.IgnoreCase);

            // ==========================================
            // INVOICE NUMBER VALIDATION & FORMATTING
            // ==========================================
            if (!string.IsNullOrWhiteSpace(model.InvoiceNo))
            {
                model.InvoiceNo = model.InvoiceNo.Trim().ToUpper();

                if (!invoiceNoRegex.IsMatch(model.InvoiceNo))
                {
                    return new
                    {
                        errorCode = 400,
                        errorMessage = "Invalid invoice number format. Format must be INV-2026-0005."
                    };
                }
            }
            else if (isEdit)
            {
                return new
                {
                    errorCode = 400,
                    errorMessage = "Invoice number is required for editing."
                };
            }

            // ==========================================
            // DATABASE TRANSACTION
            // ==========================================
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (isEdit)
                {
                    // ==========================================
                    // EDIT / UPDATE LOGIC
                    // ==========================================
                    invoice = await _context.SalesInvoices
                        .FirstOrDefaultAsync(x => x.InvoiceId == model.InvoiceId);

                    if (invoice == null)
                    {
                        await transaction.RollbackAsync();
                        return new { errorCode = 404, errorMessage = "Invoice not found for update." };
                    }

                    // DUPLICATE INVOICE CHECK FOR EDIT
                    bool invoiceExists = await _context.SalesInvoices
                        .AnyAsync(x => x.InvoiceNo == model.InvoiceNo && x.InvoiceId != model.InvoiceId);

                    if (invoiceExists)
                    {
                        await transaction.RollbackAsync();
                        return new { errorCode = 409, errorMessage = "This invoice number already exists." };
                    }

                    // UPDATE INVOICE HEADER
                    invoice.InvoiceNo = model.InvoiceNo;
                    invoice.CustomerId = model.CustomerId;
                    invoice.JobId = model.JobId;
                    invoice.TotalAmount = model.TotalAmount;
                    invoice.Advance = model.Advance;
                    invoice.Discount = model.Discount;
                    invoice.NetAmount = model.NetAmount;
                    invoice.VatAmount = model.VatAmount;
                    invoice.GrandTotal = model.GrandTotal;
                    invoice.ModifiedBy = loginId;
                    invoice.ModifiedOn = DateTime.Now;

                    _context.SalesInvoices.Update(invoice);

                    // DELETE OLD DETAILS
                    var existingDetails = await _context.SalesInvoiceDetails
                        .Where(x => x.InvoiceId == model.InvoiceId)
                        .ToListAsync();

                    if (existingDetails.Count > 0)
                    {
                        _context.SalesInvoiceDetails.RemoveRange(existingDetails);
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
                        finalInvoiceNo = model.InvoiceNo;

                        // DUPLICATE CHECK
                        bool invoiceExists = await _context.SalesInvoices
                            .AnyAsync(x => x.InvoiceNo == finalInvoiceNo);

                        if (invoiceExists)
                        {
                            await transaction.RollbackAsync();
                            return new { errorCode = 409, errorMessage = "This invoice number already exists." };
                        }

                        // EXTRACT SEQUENCE NUMBER FROM CUSTOM INVOICE NO (e.g. INV-2026-0008 -> 8)
                        var parts = finalInvoiceNo.Split('-');
                        if (parts.Length == 3 && int.TryParse(parts[2], out int customSeqNum))
                        {
                            // UPDATE SQL SEQUENCE IF CUSTOM NUMBER IS HIGHER THAN CURRENT SEQUENCE
                            await UpdateSequenceIfHigherAsync(customSeqNum);
                        }
                    }
                    else
                    {
                        // NO INVOICE NUMBER PROVIDED -> GENERATE FROM SQL SEQUENCE
                        var sequenceNumber = await GetNextSalesInvoiceSequenceNumber();
                        finalInvoiceNo = $"INV-{DateTime.Now.Year}-{sequenceNumber:D4}";
                    }

                    // CREATE INVOICE
                    invoice = new SalesInvoice
                    {
                        InvoiceNo = finalInvoiceNo,
                        CustomerId = model.CustomerId,
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

                    _context.SalesInvoices.Add(invoice);
                }

                // SAVE INVOICE HEADER
                await _context.SaveChangesAsync();

                // SAVE INVOICE DETAILS
                if (model.Items != null && model.Items.Count > 0)
                {
                    var detailsList = model.Items.Select(item => new SalesInvoiceDetail
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

                    await _context.SalesInvoiceDetails.AddRangeAsync(detailsList);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();

                if (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
                {
                    return new { errorCode = 409, errorMessage = "This invoice number already exists." };
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
                var invoiceData = await GetInvoiceById(invoice.InvoiceId);
                if (invoiceData == null) throw new Exception("Invoice data not found for PDF generation");

                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ReferenceId == _session.ReferenceId);

                if (user == null) throw new Exception("User not found.");

                dynamic data = invoiceData;
                var inv = data.invoice;
                var details = data.details;

                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, "wwwroot", "templates", "pdf", "salesinvoice.html");
                var fullHtml = await File.ReadAllTextAsync(filePath);

                var invoiceRows = new StringBuilder();
                int sr = 1;

                foreach (var item in details)
                {
                    invoiceRows.AppendFormat(
                        "<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td><td>{6}</td><td>{7}</td><td>{8}</td></tr>",
                        sr++, item.PaymentType, item.InvoiceDetail, item.Unit, item.Qty, item.Rate, item.Amount, item.VatAmount, item.TotalAmount
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
                    .Replace("{{InvoiceStatus}}", StatusHelper.GetInvoiceStatus(inv.Status) ?? "")
                    .Replace("{{CustomerName}}", inv.CustomerName ?? "")
                    .Replace("{{CustomerNameAr}}", inv.CustomerNameAr ?? "")
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
                var pdfFolder = Path.Combine(basePath, "wwwroot", "pdfs", "invoice");

                if (!Directory.Exists(pdfFolder)) Directory.CreateDirectory(pdfFolder);

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
                _logs.Write("SalesInvoice", "Save", ex.ToString());
                return new
                {
                    errorCode = 500,
                    errorMessage = ex.InnerException?.Message ?? ex.Message
                };
            }
        }

        public async Task<dynamic> Save_Bk5(SalesInvoiceVM model, int loginId)
        {
            bool isEdit = model.InvoiceId > 0;

            SalesInvoice invoice = null!;


            // ==========================================
            // INVOICE NUMBER VALIDATION
            // ONLY FOR EDIT
            // CREATE NUMBER WILL COME FROM SQL SEQUENCE
            // ==========================================

            if (isEdit)
            {
                if (string.IsNullOrWhiteSpace(model.InvoiceNo))
                {
                    return new
                    {
                        errorCode = 400,
                        errorMessage =
                            "Invoice number is required."
                    };
                }

                model.InvoiceNo =
                    model.InvoiceNo.Trim().ToUpper();

                if (!Regex.IsMatch(
                    model.InvoiceNo,
                    @"^INV-\d{4}-\d{4}$"))
                {
                    return new
                    {
                        errorCode = 400,
                        errorMessage =
                            "Invalid invoice number format. Format must be INV-2026-0005."
                    };
                }
            }


            // ==========================================
            // DATABASE TRANSACTION
            // ==========================================

            using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                if (isEdit)
                {
                    // ==========================================
                    // EDIT / UPDATE LOGIC
                    // ==========================================

                    invoice = await _context.SalesInvoices
                        .FirstOrDefaultAsync(x =>
                            x.InvoiceId == model.InvoiceId);

                    if (invoice == null)
                    {
                        await transaction.RollbackAsync();

                        return new
                        {
                            errorCode = 404,
                            errorMessage =
                                "Invoice not found for update."
                        };
                    }


                    // ==========================================
                    // DUPLICATE INVOICE CHECK
                    // ==========================================

                    bool invoiceExists =
                        await _context.SalesInvoices
                            .AnyAsync(x =>
                                x.InvoiceNo == model.InvoiceNo &&
                                x.InvoiceId != model.InvoiceId);

                    if (invoiceExists)
                    {
                        await transaction.RollbackAsync();

                        return new
                        {
                            errorCode = 409,
                            errorMessage =
                                "This invoice number already exists."
                        };
                    }


                    // ==========================================
                    // UPDATE INVOICE HEADER
                    // ==========================================

                    invoice.InvoiceNo =
                        model.InvoiceNo;

                    invoice.CustomerId =
                        model.CustomerId;

                    invoice.JobId =
                        model.JobId;

                    invoice.TotalAmount =
                        model.TotalAmount;

                    invoice.Advance =
                        model.Advance;

                    invoice.Discount =
                        model.Discount;

                    invoice.NetAmount =
                        model.NetAmount;

                    invoice.VatAmount =
                        model.VatAmount;

                    invoice.GrandTotal =
                        model.GrandTotal;

                    invoice.ModifiedBy =
                        loginId;

                    invoice.ModifiedOn =
                        DateTime.Now;

                    _context.SalesInvoices.Update(invoice);


                    // ==========================================
                    // DELETE OLD DETAILS
                    // ==========================================

                    var existingDetails =
                        await _context.SalesInvoiceDetails
                            .Where(x =>
                                x.InvoiceId == model.InvoiceId)
                            .ToListAsync();

                    if (existingDetails.Count > 0)
                    {
                        _context.SalesInvoiceDetails
                            .RemoveRange(existingDetails);
                    }
                }
                else
                {
                    // ==========================================
                    // CREATE LOGIC
                    // ==========================================


                    // ==========================================
                    // GET UNIQUE NUMBER FROM SQL SEQUENCE
                    // ==========================================

                    var sequenceNumber =
                        await GetNextSalesInvoiceSequenceNumber();


                    // ==========================================
                    // GENERATE INVOICE NUMBER
                    // Example: INV-2026-0006
                    // ==========================================

                    var generatedInvoiceNo =
                        $"INV-{DateTime.Now.Year}-{sequenceNumber:D4}";


                    // ==========================================
                    // CREATE INVOICE
                    // ==========================================

                    invoice = new SalesInvoice
                    {
                        InvoiceNo =
                            generatedInvoiceNo,

                        CustomerId =
                            model.CustomerId,

                        JobId =
                            model.JobId,

                        InvoiceDate =
                            DateTime.Now,

                        TotalAmount =
                            model.TotalAmount,

                        Advance =
                            model.Advance,

                        Discount =
                            model.Discount,

                        NetAmount =
                            model.NetAmount,

                        VatAmount =
                            model.VatAmount,

                        GrandTotal =
                            model.GrandTotal,

                        Status = 0,

                        ReferenceId =
                            _session.ReferenceId,

                        CreatedBy =
                            loginId,

                        CreatedOn =
                            DateTime.Now
                    };

                    _context.SalesInvoices
                        .Add(invoice);
                }


                // ==========================================
                // SAVE INVOICE HEADER FIRST
                // ==========================================

                await _context.SaveChangesAsync();


                // ==========================================
                // SAVE INVOICE DETAILS
                // ==========================================

                if (model.Items != null &&
                    model.Items.Count > 0)
                {
                    var detailsList =
                        model.Items.Select(item =>
                            new SalesInvoiceDetail
                            {
                                InvoiceId =
                                    invoice.InvoiceId,

                                PaymentTypeId =
                                    item.PaymentTypeId,

                                PaymentHeaderId =
                                    item.PaymentHeaderId,

                                Qty =
                                    item.Qty,

                                Rate =
                                    item.Rate,

                                Amount =
                                    item.Amount,

                                VatPercent =
                                    item.VatPercent,

                                VatAmount =
                                    item.VatAmount,

                                TotalAmount =
                                    item.TotalAmount
                            });

                    await _context.SalesInvoiceDetails
                        .AddRangeAsync(detailsList);
                }


                // ==========================================
                // SAVE INVOICE DETAILS
                // ==========================================

                await _context.SaveChangesAsync();


                // ==========================================
                // COMMIT TRANSACTION
                // ==========================================

                await transaction.CommitAsync();
            }

            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();


                // ==========================================
                // SQL SERVER DUPLICATE KEY ERROR
                // ==========================================

                if (ex.InnerException is SqlException sqlEx &&
                    (sqlEx.Number == 2601 ||
                     sqlEx.Number == 2627))
                {
                    return new
                    {
                        errorCode = 409,
                        errorMessage =
                            "This invoice number already exists."
                    };
                }

                throw;
            }

            catch
            {
                await transaction.RollbackAsync();

                throw;
            }


            // ==========================================
            // POST TRANSACTION WORK
            // PDF GENERATION
            // ==========================================

            try
            {
                var invoiceData =
                    await GetInvoiceById(
                        invoice.InvoiceId);


                if (invoiceData == null)
                {
                    throw new Exception(
                        "Invoice data not found for PDF generation");
                }


                // ==========================================
                // GET USER
                // ==========================================

                var user =
                    await _context.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x =>
                            x.ReferenceId ==
                            _session.ReferenceId);


                if (user == null)
                {
                    throw new Exception(
                        "User not found.");
                }


                // ==========================================
                // GET INVOICE DATA
                // ==========================================

                dynamic data =
                    invoiceData;

                var inv =
                    data.invoice;

                var details =
                    data.details;


                // ==========================================
                // LOAD HTML TEMPLATE
                // ==========================================

                var basePath =
                    Directory.GetCurrentDirectory();


                var filePath =
                    Path.Combine(
                        basePath,
                        "wwwroot",
                        "templates",
                        "pdf",
                        "salesinvoice.html"
                    );


                var fullHtml =
                    await File.ReadAllTextAsync(
                        filePath);


                // ==========================================
                // BUILD INVOICE ROWS
                // ==========================================

                var invoiceRows =
                    new StringBuilder();


                int sr = 1;


                foreach (var item in details)
                {
                    invoiceRows.AppendFormat(
                        "<tr>" +
                        "<td>{0}</td>" +
                        "<td>{1}</td>" +
                        "<td>{2}</td>" +
                        "<td>{3}</td>" +
                        "<td>{4}</td>" +
                        "<td>{5}</td>" +
                        "<td>{6}</td>" +
                        "<td>{7}</td>" +
                        "<td>{8}</td>" +
                        "</tr>",

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


                // ==========================================
                // REPLACE HTML PLACEHOLDERS
                // ==========================================

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
                    .Replace("{{InvoiceStatus}}", StatusHelper.GetInvoiceStatus(inv.Status) ?? "")
                    .Replace("{{CustomerName}}", inv.CustomerName ?? "")
                    .Replace("{{CustomerNameAr}}", inv.CustomerNameAr ?? "")
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


                // ==========================================
                // GENERATE PDF
                // ==========================================

                var pdfBytes =
                    GeneratePdfFromHtml(
                        fullHtml);


                // ==========================================
                // PDF FOLDER
                // ==========================================

                var pdfFolder =
                    Path.Combine(
                        basePath,
                        "wwwroot",
                        "pdfs",
                        "invoice"
                    );


                if (!Directory.Exists(pdfFolder))
                {
                    Directory.CreateDirectory(
                        pdfFolder);
                }


                // ==========================================
                // SAVE PDF
                // ==========================================

                var pdfPath =
                    Path.Combine(
                        pdfFolder,
                        $"{invoice.InvoiceNo}.pdf"
                    );


                await File.WriteAllBytesAsync(
                    pdfPath,
                    pdfBytes);


                // ==========================================
                // SUCCESS RESPONSE
                // ==========================================

                return new
                {
                    errorCode = 200,

                    message =
                        isEdit
                            ? "Invoice Updated Successfully"
                            : "Invoice Saved Successfully",

                    invoiceId =
                        invoice.InvoiceId,

                    invoiceNo =
                        invoice.InvoiceNo
                };
            }

            catch (Exception ex)
            {
                _logs.Write(
                    "SalesInvoice",
                    "Save",
                    ex.ToString()
                );


                return new
                {
                    errorCode = 500,

                    errorMessage =
                        ex.InnerException?.Message ??
                        ex.Message
                };
            }
        }

        public async Task<dynamic> Save_Bk4(SalesInvoiceVM model, int loginId)
        {
            bool isEdit = model.InvoiceId > 0;
            SalesInvoice invoice = null!;

            // ==========================================
            // INVOICE NUMBER VALIDATION
            // ONLY FOR EDIT
            // CREATE: InvoiceNo will be generated
            // automatically from SQL Server SEQUENCE
            // ==========================================

            if (isEdit)
            {
                if (string.IsNullOrWhiteSpace(model.InvoiceNo))
                {
                    return new
                    {
                        errorCode = 400,
                        errorMessage = "Invoice number is required."
                    };
                }

                model.InvoiceNo = model.InvoiceNo.Trim().ToUpper();

                if (!Regex.IsMatch(
                    model.InvoiceNo,
                    @"^INV-\d{4}-\d{4}$"))
                {
                    return new
                    {
                        errorCode = 400,
                        errorMessage =
                            "Invalid invoice number format. Format must be INV-2026-0005."
                    };
                }
            }


            // ==========================================
            // DATABASE TRANSACTION
            // ==========================================

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    if (isEdit)
                    {
                        // ==========================================
                        // EDIT / UPDATE LOGIC
                        // ==========================================

                        invoice = await _context.SalesInvoices
                            .FirstOrDefaultAsync(x =>
                                x.InvoiceId == model.InvoiceId);

                        if (invoice == null)
                        {
                            return new
                            {
                                errorCode = 404,
                                errorMessage =
                                    "Invoice not found for update."
                            };
                        }


                        // ==========================================
                        // DUPLICATE INVOICE CHECK
                        // Exclude current invoice
                        // ==========================================

                        bool invoiceExists = await _context.SalesInvoices
                            .AnyAsync(x =>
                                x.InvoiceNo == model.InvoiceNo &&
                                x.InvoiceId != model.InvoiceId);

                        if (invoiceExists)
                        {
                            return new
                            {
                                errorCode = 409,
                                errorMessage =
                                    "This invoice number already exists."
                            };
                        }


                        // ==========================================
                        // UPDATE HEADER
                        // ==========================================

                        invoice.InvoiceNo = model.InvoiceNo;

                        invoice.CustomerId = model.CustomerId;
                        invoice.JobId = model.JobId;

                        invoice.TotalAmount = model.TotalAmount;
                        invoice.Advance = model.Advance;
                        invoice.Discount = model.Discount;
                        invoice.NetAmount = model.NetAmount;
                        invoice.VatAmount = model.VatAmount;
                        invoice.GrandTotal = model.GrandTotal;

                        invoice.ModifiedBy = loginId;
                        invoice.ModifiedOn = DateTime.Now;

                        _context.SalesInvoices.Update(invoice);


                        // ==========================================
                        // DELETE OLD DETAILS
                        // ==========================================

                        var existingDetails =
                            await _context.SalesInvoiceDetails
                                .Where(x =>
                                    x.InvoiceId == model.InvoiceId)
                                .ToListAsync();

                        if (existingDetails.Count > 0)
                        {
                            _context.SalesInvoiceDetails
                                .RemoveRange(existingDetails);
                        }
                    }
                    else
                    {
                        // ==========================================
                        // CREATE LOGIC
                        // ==========================================


                        // ==========================================
                        // GET NEXT NUMBER FROM SQL SERVER SEQUENCE
                        // ==========================================

                        var sequenceNumber =
                            await _context.Database
                                .SqlQueryRaw<long>(
                                    "SELECT NEXT VALUE FOR dbo.SalesInvoiceSequence AS Value"
                                )
                                .SingleAsync();


                        // ==========================================
                        // GENERATE INVOICE NUMBER
                        // Example:
                        // INV-2026-0001
                        // ==========================================

                        var generatedInvoiceNo =
                            $"INV-{DateTime.Now.Year}-{sequenceNumber:D4}";


                        // ==========================================
                        // CREATE INVOICE
                        // InvoiceId is generated automatically
                        // by SQL Server IDENTITY
                        // ==========================================

                        invoice = new SalesInvoice
                        {
                            InvoiceNo = generatedInvoiceNo,

                            CustomerId = model.CustomerId,
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

                        _context.SalesInvoices.Add(invoice);
                    }


                    // ==========================================
                    // SAVE HEADER FIRST
                    // InvoiceId will be generated by SQL Server
                    // ==========================================

                    await _context.SaveChangesAsync();


                    // ==========================================
                    // SAVE INVOICE DETAILS
                    // ==========================================

                    if (model.Items != null &&
                        model.Items.Count > 0)
                    {
                        var detailsList = model.Items.Select(item =>
                            new SalesInvoiceDetail
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

                        await _context.SalesInvoiceDetails
                            .AddRangeAsync(detailsList);
                    }


                    // ==========================================
                    // SAVE DETAILS
                    // ==========================================

                    await _context.SaveChangesAsync();


                    // ==========================================
                    // COMMIT TRANSACTION
                    // ==========================================

                    await transaction.CommitAsync();
                }

                catch (DbUpdateException ex)
                {
                    await transaction.RollbackAsync();


                    // ==========================================
                    // SQL SERVER DUPLICATE KEY ERROR
                    // ==========================================

                    if (ex.InnerException is SqlException sqlEx &&
                        (sqlEx.Number == 2601 ||
                         sqlEx.Number == 2627))
                    {
                        return new
                        {
                            errorCode = 409,
                            errorMessage =
                                "This invoice number already exists."
                        };
                    }

                    throw;
                }

                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }


            // ==========================================
            // POST-TRANSACTION WORK
            // PDF GENERATION
            // ==========================================

            try
            {
                var invoiceData =
                    await GetInvoiceById(invoice.InvoiceId);

                if (invoiceData == null)
                {
                    throw new Exception(
                        "Invoice data not found for PDF generation");
                }


                // ==========================================
                // GET USER
                // ==========================================

                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ReferenceId == _session.ReferenceId);

                if (user == null)
                {
                    throw new Exception("User not found.");
                }


                // ==========================================
                // GET INVOICE DATA
                // ==========================================

                dynamic data = invoiceData;

                var inv = data.invoice;
                var details = data.details;


                // ==========================================
                // LOAD HTML TEMPLATE
                // ==========================================

                var basePath =
                    Directory.GetCurrentDirectory();

                var filePath = Path.Combine(
                    basePath,
                    "wwwroot",
                    "templates",
                    "pdf",
                    "salesinvoice.html"
                );

                var fullHtml =
                    await File.ReadAllTextAsync(filePath);


                // ==========================================
                // BUILD INVOICE ROWS
                // ==========================================

                var invoiceRows =
                    new StringBuilder();

                int sr = 1;

                foreach (var item in details)
                {
                    invoiceRows.AppendFormat(
                        "<tr>" +
                        "<td>{0}</td>" +
                        "<td>{1}</td>" +
                        "<td>{2}</td>" +
                        "<td>{3}</td>" +
                        "<td>{4}</td>" +
                        "<td>{5}</td>" +
                        "<td>{6}</td>" +
                        "<td>{7}</td>" +
                        "<td>{8}</td>" +
                        "</tr>",

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


                // ==========================================
                // REPLACE HTML PLACEHOLDERS
                // ==========================================

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
                    .Replace("{{InvoiceStatus}}", StatusHelper.GetInvoiceStatus(inv.Status) ?? "")
                    .Replace("{{CustomerName}}", inv.CustomerName ?? "")
                    .Replace("{{CustomerNameAr}}", inv.CustomerNameAr ?? "")
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


                // ==========================================
                // GENERATE PDF
                // ==========================================

                var pdfBytes =
                    GeneratePdfFromHtml(fullHtml);


                // ==========================================
                // PDF FOLDER
                // ==========================================

                var pdfFolder = Path.Combine(
                    basePath,
                    "wwwroot",
                    "pdfs",
                    "invoice"
                );

                if (!Directory.Exists(pdfFolder))
                {
                    Directory.CreateDirectory(pdfFolder);
                }


                // ==========================================
                // SAVE PDF
                // ==========================================

                var pdfPath = Path.Combine(
                    pdfFolder,
                    $"{invoice.InvoiceNo}.pdf"
                );

                await File.WriteAllBytesAsync(
                    pdfPath,
                    pdfBytes
                );


                // ==========================================
                // SUCCESS RESPONSE
                // ==========================================

                return new
                {
                    errorCode = 200,

                    message = isEdit
                        ? "Invoice Updated Successfully"
                        : "Invoice Saved Successfully",

                    invoiceId = invoice.InvoiceId,
                    invoiceNo = invoice.InvoiceNo
                };
            }

            catch (Exception ex)
            {
                _logs.Write(
                    "SalesInvoice",
                    "Save",
                    ex.ToString()
                );

                return new
                {
                    errorCode = 500,
                    errorMessage =
                        ex.InnerException?.Message ??
                        ex.Message
                };
            }
        }

        public async Task<dynamic> Save_Bk3(SalesInvoiceVM model, int loginId)
        {
            bool isEdit = model.InvoiceId > 0;
            SalesInvoice invoice = null!;

            // ==========================================
            // INVOICE NUMBER FORMAT VALIDATION
            // Format: INV-2026-0005
            // ==========================================

            if (string.IsNullOrWhiteSpace(model.InvoiceNo))
            {
                return new
                {
                    errorCode = 400,
                    errorMessage = "Invoice number is required."
                };
            }

            model.InvoiceNo = model.InvoiceNo.Trim().ToUpper();

            if (!Regex.IsMatch(model.InvoiceNo, @"^INV-\d{4}-\d{4}$"))
            {
                return new
                {
                    errorCode = 400,
                    errorMessage = "Invalid invoice number format. Format must be INV-2026-0005."
                };
            }


            // ==========================================
            // DATABASE TRANSACTION
            // ==========================================

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    if (isEdit)
                    {
                        // ==========================================
                        // EDIT / UPDATE LOGIC
                        // ==========================================

                        invoice = await _context.SalesInvoices
                            .FirstOrDefaultAsync(x =>
                                x.InvoiceId == model.InvoiceId);

                        if (invoice == null)
                        {
                            return new
                            {
                                errorCode = 404,
                                errorMessage = "Invoice not found for update."
                            };
                        }


                        // ==========================================
                        // DUPLICATE INVOICE CHECK
                        // Exclude current invoice
                        // ==========================================

                        bool invoiceExists = await _context.SalesInvoices
                            .AnyAsync(x =>
                                x.InvoiceNo == model.InvoiceNo &&
                                x.InvoiceId != model.InvoiceId);

                        if (invoiceExists)
                        {
                            return new
                            {
                                errorCode = 409,
                                errorMessage = "This invoice number already exists."
                            };
                        }


                        // ==========================================
                        // UPDATE HEADER
                        // ==========================================

                        invoice.InvoiceNo = model.InvoiceNo;

                        invoice.CustomerId = model.CustomerId;
                        invoice.JobId = model.JobId;

                        invoice.TotalAmount = model.TotalAmount;
                        invoice.Advance = model.Advance;
                        invoice.Discount = model.Discount;
                        invoice.NetAmount = model.NetAmount;
                        invoice.VatAmount = model.VatAmount;
                        invoice.GrandTotal = model.GrandTotal;

                        invoice.ModifiedBy = loginId;
                        invoice.ModifiedOn = DateTime.Now;

                        _context.SalesInvoices.Update(invoice);


                        // ==========================================
                        // DELETE OLD DETAILS
                        // ==========================================

                        var existingDetails = await _context.SalesInvoiceDetails
                            .Where(x => x.InvoiceId == model.InvoiceId)
                            .ToListAsync();

                        if (existingDetails.Count > 0)
                        {
                            _context.SalesInvoiceDetails
                                .RemoveRange(existingDetails);
                        }
                    }
                    else
                    {
                        // ==========================================
                        // CREATE LOGIC
                        // ==========================================

                        // Optional check for user-friendly message
                        bool invoiceExists = await _context.SalesInvoices
                            .AnyAsync(x =>
                                x.InvoiceNo == model.InvoiceNo);

                        if (invoiceExists)
                        {
                            return new
                            {
                                errorCode = 409,
                                errorMessage = "This invoice number already exists."
                            };
                        }


                        // ==========================================
                        // CREATE INVOICE
                        // InvoiceId should be SQL Identity
                        // ==========================================

                        invoice = new SalesInvoice
                        {
                            InvoiceNo = model.InvoiceNo,

                            CustomerId = model.CustomerId,
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

                        _context.SalesInvoices.Add(invoice);
                    }


                    // ==========================================
                    // SAVE HEADER FIRST
                    //
                    // Important for CREATE because InvoiceId
                    // is generated by SQL Server Identity
                    // ==========================================

                    await _context.SaveChangesAsync();


                    // ==========================================
                    // SAVE INVOICE DETAILS
                    // ==========================================

                    if (model.Items != null && model.Items.Count > 0)
                    {
                        var detailsList = model.Items.Select(item =>
                            new SalesInvoiceDetail
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

                        await _context.SalesInvoiceDetails
                            .AddRangeAsync(detailsList);
                    }


                    // ==========================================
                    // SAVE DETAILS
                    // ==========================================

                    await _context.SaveChangesAsync();


                    // ==========================================
                    // COMMIT TRANSACTION
                    // ==========================================

                    await transaction.CommitAsync();
                }

                catch (DbUpdateException ex)
                {
                    await transaction.RollbackAsync();

                    // SQL Server Duplicate Key Error
                    if (ex.InnerException is SqlException sqlEx &&
                        (sqlEx.Number == 2601 || sqlEx.Number == 2627))
                    {
                        return new
                        {
                            errorCode = 409,
                            errorMessage =
                                "This invoice number already exists. Please use a different invoice number."
                        };
                    }

                    throw;
                }

                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }


            // ==========================================
            // POST-TRANSACTION WORK
            // PDF GENERATION
            // ==========================================

            try
            {
                var invoiceData =
                    await GetInvoiceById(invoice.InvoiceId);

                if (invoiceData == null)
                {
                    throw new Exception(
                        "Invoice data not found for PDF generation");
                }

                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ReferenceId == _session.ReferenceId);

                if (user == null)
                {
                    throw new Exception("User not found.");
                }


                dynamic data = invoiceData;

                var inv = data.invoice;
                var details = data.details;


                // ==========================================
                // LOAD HTML TEMPLATE
                // ==========================================

                var basePath = Directory.GetCurrentDirectory();

                var filePath = Path.Combine(
                    basePath,
                    "wwwroot",
                    "templates",
                    "pdf",
                    "salesinvoice.html"
                );

                var fullHtml =
                    await File.ReadAllTextAsync(filePath);


                // ==========================================
                // BUILD INVOICE ROWS
                // ==========================================

                var invoiceRows = new StringBuilder();

                int sr = 1;

                foreach (var item in details)
                {
                    invoiceRows.AppendFormat(
                        "<tr>" +
                        "<td>{0}</td>" +
                        "<td>{1}</td>" +
                        "<td>{2}</td>" +
                        "<td>{3}</td>" +
                        "<td>{4}</td>" +
                        "<td>{5}</td>" +
                        "<td>{6}</td>" +
                        "<td>{7}</td>" +
                        "<td>{8}</td>" +
                        "</tr>",

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


                // ==========================================
                // REPLACE HTML PLACEHOLDERS
                // ==========================================

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
                    .Replace(
                        "{{InvoiceDate}}",
                        Convert.ToDateTime(inv.InvoiceDate)
                            .ToString("dd-MMM-yyyy")
                    )
                    .Replace(
                        "{{InvoiceStatus}}",
                        StatusHelper.GetInvoiceStatus(inv.Status) ?? ""
                    )
                    .Replace("{{CustomerName}}", inv.CustomerName ?? "")
                    .Replace("{{CustomerNameAr}}", inv.CustomerNameAr ?? "")
                    .Replace("{{CustomerAddress}}", inv.CustomerAddress ?? "")
                    .Replace(
                        "{{CustomerCity}}",
                        $"{inv.RegionName} {inv.CountryName}"
                    )
                    .Replace("{{CustomerVAT}}", inv.CustomerVat ?? "")
                    .Replace("{{JobNumber}}", inv.JobNumber ?? "")
                    .Replace("{{ModeOfShipment}}", inv.ModeOfShipment ?? "")
                    .Replace("{{ShipmentType}}", inv.ShipmentType ?? "")
                    .Replace("{{POL}}", inv.POL ?? "")
                    .Replace("{{POD}}", inv.POD ?? "")
                    .Replace("{{BLNumber}}", inv.BLNumber ?? "")
                    .Replace("{{PaymentTerms}}", "")
                    .Replace("{{InvoiceRows}}", invoiceRows.ToString())
                    .Replace(
                        "{{Total}}",
                        inv.TotalAmount?.ToString("N2") ?? "0"
                    )
                    .Replace(
                        "{{Advance}}",
                        inv.Advance?.ToString("N2") ?? "0"
                    )
                    .Replace(
                        "{{Discount}}",
                        inv.Discount?.ToString("N2") ?? "0"
                    )
                    .Replace(
                        "{{NetAmount}}",
                        inv.NetAmount?.ToString("N2") ?? "0"
                    )
                    .Replace(
                        "{{VAT}}",
                        inv.VatAmount?.ToString("N2") ?? "0"
                    )
                    .Replace(
                        "{{GrandTotal}}",
                        inv.GrandTotal?.ToString("N2") ?? "0"
                    )
                    .Replace(
                        "{{AmountInWords}}",
                        inv.AmountInWords ?? ""
                    )
                    .Replace(
                        "{{AccountNo}}",
                        inv.AccountNumber ?? ""
                    )
                    .Replace(
                        "{{BankName}}",
                        inv.BankName ?? ""
                    )
                    .Replace(
                        "{{IBAN}}",
                        inv.IBAN ?? ""
                    )
                    .Replace(
                        "{{PrintDate}}",
                        DateTime.Now.ToString("dd-MMM-yyyy hh:mm tt")
                    )
                    .Replace("{{QRCodeValue}}", "");


                // ==========================================
                // GENERATE PDF
                // ==========================================

                var pdfBytes =
                    GeneratePdfFromHtml(fullHtml);

                var pdfFolder = Path.Combine(
                    basePath,
                    "wwwroot",
                    "pdfs",
                    "invoice"
                );

                if (!Directory.Exists(pdfFolder))
                {
                    Directory.CreateDirectory(pdfFolder);
                }


                // ==========================================
                // SAVE PDF
                // ==========================================

                var pdfPath = Path.Combine(
                    pdfFolder,
                    $"{invoice.InvoiceNo}.pdf"
                );

                await File.WriteAllBytesAsync(
                    pdfPath,
                    pdfBytes
                );


                return new
                {
                    errorCode = 200,

                    message = isEdit
                        ? "Invoice Updated Successfully"
                        : "Invoice Saved Successfully",

                    invoiceId = invoice.InvoiceId,
                    invoiceNo = invoice.InvoiceNo
                };
            }

            catch (Exception ex)
            {
                _logs.Write(
                    "SalesInvoice",
                    "Save",
                    ex.ToString()
                );

                return new
                {
                    errorCode = 500,
                    errorMessage =
                        ex.InnerException?.Message ?? ex.Message
                };
            }
        }

        public async Task<dynamic> Save_Bk2(SalesInvoiceVM model, int loginId)
        {
            bool isEdit = model.InvoiceId > 0;
            SalesInvoice invoice = null!;

            // 1. DATABASE TRANSACTION (Keep it as small as possible)
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // DUPLICATE JOB CHECK (Exclude self on edit)
                    //var alreadyExists = await _context.SalesInvoices
                    //    .AnyAsync(x => x.JobId == model.JobId && (!isEdit || x.InvoiceId != model.InvoiceId));

                    //if (alreadyExists)
                    //{
                    //    return new
                    //    {
                    //        errorCode = 409,
                    //        errorMessage = "Invoice already created against this job."
                    //    };
                    //}

                    if (isEdit)
                    {
                        // ==========================================
                        // EDIT / UPDATE LOGIC
                        // ==========================================
                        invoice = await _context.SalesInvoices
                            .FirstOrDefaultAsync(x => x.InvoiceId == model.InvoiceId);

                        if (invoice == null)
                        {
                            return new
                            {
                                errorCode = 404,
                                errorMessage = "Invoice not found for update."
                            };
                        }

                        // Update Header Properties
                        invoice.CustomerId = model.CustomerId;
                        invoice.JobId = model.JobId;
                        invoice.TotalAmount = model.TotalAmount;
                        invoice.Advance = model.Advance;
                        invoice.Discount = model.Discount;
                        invoice.NetAmount = model.NetAmount;
                        invoice.VatAmount = model.VatAmount;
                        invoice.GrandTotal = model.GrandTotal;
                        invoice.ModifiedBy = loginId;
                        invoice.ModifiedOn = DateTime.Now;

                        _context.SalesInvoices.Update(invoice);

                        // Batch delete old details without loading entities into tracking memory if using ExecuteDeleteAsync (.NET 7+)
                        // Otherwise clear tracking memory list:
                        var existingDetails = await _context.SalesInvoiceDetails
                            .Where(x => x.InvoiceId == model.InvoiceId)
                            .ToListAsync();

                        if (existingDetails.Count > 0)
                        {
                            _context.SalesInvoiceDetails.RemoveRange(existingDetails);
                        }
                    }
                    else
                    {
                        // ==========================================
                        // CREATE LOGIC
                        // ==========================================
                        var maxId = await _context.SalesInvoices.MaxAsync(c => (int?)c.InvoiceId) ?? 0;
                        int newId = maxId + 1;

                        string invoiceNo = $"INV-{DateTime.Now.Year}-{newId:D4}";

                        invoice = new SalesInvoice
                        {
                            InvoiceId = newId,
                            InvoiceNo = invoiceNo,
                            CustomerId = model.CustomerId,
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
                            CreatedOn = DateTime.Now,
                        };

                        _context.SalesInvoices.Add(invoice);
                    }

                    // Save Details using AddRange for single bulk insertion
                    if (model.Items != null && model.Items.Count > 0)
                    {
                        var detailsList = model.Items.Select(item => new SalesInvoiceDetail
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

                        await _context.SalesInvoiceDetails.AddRangeAsync(detailsList);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync(); // Released DB locks early!
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw; // Handled by outer catch
                }
            }

            // 2. POST-TRANSACTION WORK (PDF Generation & Template Processing)
            try
            {
                var invoiceData = await GetInvoiceById(invoice.InvoiceId);
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

                // LOAD HTML TEMPLATE
                var basePath = Directory.GetCurrentDirectory();
                var filePath = Path.Combine(basePath, "wwwroot", "templates", "pdf", "salesinvoice.html");

                var fullHtml = await File.ReadAllTextAsync(filePath);

                // OPTIMIZED STRING BUILDER (No unnecessary string allocations)
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

                // REPLACING PLACEHOLDERS
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
                    .Replace("{{InvoiceStatus}}", StatusHelper.GetInvoiceStatus(inv.Status) ?? "")
                    .Replace("{{CustomerName}}", inv.CustomerName ?? "")
                    .Replace("{{CustomerNameAr}}", inv.CustomerNameAr ?? "")
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

                // PDF FILE SAVING
                var pdfBytes = GeneratePdfFromHtml(fullHtml);
                var pdfFolder = Path.Combine(basePath, "wwwroot", "pdfs", "invoice");

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
                _logs.Write("SalesInvoice", "Save", ex.ToString());

                return new
                {
                    errorCode = 500,
                    errorMessage = ex.InnerException?.Message ?? ex.Message
                };
            }
        }

        public async Task<dynamic> Save_Bk(SalesInvoiceVM model, int loginId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var alreadyExists = await _context.SalesInvoices
                    .AnyAsync(x => x.JobId == model.JobId);

                if (alreadyExists)
                {
                    return new
                    {
                        errorCode = 409,
                        errorMessage = "Invoice already created against this job."
                    };
                }

                // INVOICE NO
                //int count = await _context.SalesInvoices.CountAsync();

                var maxId = await _context.SalesInvoices.MaxAsync(c => (int?)c.InvoiceId) ?? 0;
                int newId = maxId + 1;

                string invoiceNo =
                    "INV-" + DateTime.Now.Year + "-" + (newId).ToString("D4");

                // SAVE HEADER
                var invoice = new SalesInvoice
                {
                    InvoiceId = newId,
                    InvoiceNo = invoiceNo,
                    CustomerId = model.CustomerId,
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
                    CreatedOn = DateTime.Now,
                };

                _context.SalesInvoices.Add(invoice);
                await _context.SaveChangesAsync();

                // SAVE DETAILS
                foreach (var item in model.Items)
                {
                    _context.SalesInvoiceDetails.Add(new SalesInvoiceDetail
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

                // =====================================================
                // 🔥 FETCH FULL DATA USING YOUR GET INVOICE QUERY LOGIC
                // =====================================================

                var invoiceData = await GetInvoiceById(invoice.InvoiceId);

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

                // =====================================================
                // LOAD HTML TEMPLATE
                // =====================================================

                var basePath = Directory.GetCurrentDirectory();

                var filePath = Path.Combine(
                    basePath,
                    "wwwroot",
                    "templates",
                    "pdf",
                    "salesinvoice.html"
                );

                var fullHtml = await File.ReadAllTextAsync(filePath);

                // =====================================================
                // BUILD INVOICE ROWS
                // =====================================================

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

                // =====================================================
                // REPLACE VARIABLES (FROM GetInvoiceById RESULT)
                // =====================================================

                fullHtml = fullHtml.Replace("{{CompanyName}}", user.CompanyName ?? "");
                fullHtml = fullHtml.Replace("{{CompanyNameAr}}", user.CompanyNameAr ?? "");

                fullHtml = fullHtml.Replace("{{EstablishmentName}}", user.EstablishmentName ?? "");
                fullHtml = fullHtml.Replace("{{EstablishmentNameAr}}", user.EstablishmentNameAr ?? "");

                fullHtml = fullHtml.Replace("{{Country}}", user.Country ?? "");
                fullHtml = fullHtml.Replace("{{CountryAr}}", user.CountryAr ?? "");

                fullHtml = fullHtml.Replace("{{City}}", user.City ?? "");
                fullHtml = fullHtml.Replace("{{CityAr}}", user.CityAr ?? "");

                fullHtml = fullHtml.Replace("{{VATNumber}}", user.Vatnumber ?? "");

                fullHtml = fullHtml.Replace("{{InvoiceNo}}", inv.InvoiceNo ?? "");
                fullHtml = fullHtml.Replace("{{InvoiceDate}}", Convert.ToDateTime(inv.InvoiceDate).ToString("dd-MMM-yyyy"));
                fullHtml = fullHtml.Replace("{{InvoiceStatus}}", StatusHelper.GetInvoiceStatus(inv.Status) ?? "");

                fullHtml = fullHtml.Replace("{{CustomerName}}", inv.CustomerName ?? "");
                fullHtml = fullHtml.Replace("{{CustomerNameAr}}", inv.CustomerNameAr ?? "");
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

                // TABLE
                fullHtml = fullHtml.Replace("{{InvoiceRows}}", invoiceRows);

                // SUMMARY
                fullHtml = fullHtml.Replace("{{Total}}", inv.TotalAmount?.ToString("N2") ?? "0");
                fullHtml = fullHtml.Replace("{{Advance}}", inv.Advance?.ToString("N2") ?? "0");
                fullHtml = fullHtml.Replace("{{Discount}}", inv.Discount?.ToString("N2") ?? "0");
                fullHtml = fullHtml.Replace("{{NetAmount}}", inv.NetAmount?.ToString("N2") ?? "0");
                fullHtml = fullHtml.Replace("{{VAT}}", inv.VatAmount?.ToString("N2") ?? "0");
                fullHtml = fullHtml.Replace("{{GrandTotal}}", inv.GrandTotal?.ToString("N2") ?? "0");

                fullHtml = fullHtml.Replace("{{AmountInWords}}", inv.AmountInWords ?? "");

                // BANK DETAILS
                fullHtml = fullHtml.Replace("{{AccountNo}}", inv.AccountNumber ?? "");
                fullHtml = fullHtml.Replace("{{BankName}}", inv.BankName ?? "");
                fullHtml = fullHtml.Replace("{{IBAN}}", inv.IBAN ?? "");

                // OTHER DETAILS
                fullHtml = fullHtml.Replace("{{PrintDate}}", DateTime.Now.ToString("dd-MMM-yyyy hh:mm tt"));
                fullHtml = fullHtml.Replace("{{QRCodeValue}}", "");

                // COMPANY DETAILS



                // =====================================================
                // GENERATE PDF
                // =====================================================

                var pdfBytes = GeneratePdfFromHtml(fullHtml);

                // =====================================================
                // SAVE PDF FILE
                // =====================================================

                var pdfFolder = Path.Combine(basePath, "wwwroot", "pdfs", "invoice");

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

                //_logs.Write(
                //    "SalesInvoice",
                //    "Save",
                //    ex.InnerException?.Message ?? ex.Message
                //);

                _logs.Write(
                    "SalesInvoice",
                    "Save",
                    ex.ToString()
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

                int customerId = 0;
                int.TryParse(filters.First(x => x.fieldName == "CustomerId").filterValue, out customerId);

                DateTime? fromDate = null;
                DateTime? toDate = null;

                if (DateTime.TryParse(filters.First(x => x.fieldName == "FromDate").filterValue, out DateTime f))
                    fromDate = f;

                if (DateTime.TryParse(filters.First(x => x.fieldName == "ToDate").filterValue, out DateTime t))
                    toDate = t;

                var query =
                    from inv in _context.SalesInvoices

                    join c in _context.Customers
                        on inv.CustomerId equals c.Id into custJoin
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
                        CustomerId = c.Id,
                        CustomerName = c.CustomerName,
                        CustomerNameAr = c.CustomerNameAr,
                        inv.InvoiceDate,
                        inv.NetAmount,
                        inv.TotalAmount,
                        inv.VatAmount,
                        inv.GrandTotal,
                        inv.PaidAmount,
                        inv.BalanceAmount,
                        inv.Status,
                        inv.IsCancelled,
                        StatusName = StatusHelper.GetInvoiceStatus(inv.Status),
                        CreatedByUser = u.UserName,
                        inv.ReferenceId
                    };


                query = query.Where(x => x.ReferenceId == _session.ReferenceId);

                // Filters
                if (!string.IsNullOrEmpty(invoiceNo))
                    query = query.Where(x => x.InvoiceNo.Contains(invoiceNo));

                if (jobId > 0)
                    query = query.Where(x => x.JobId == jobId);

                if (customerId > 0)
                    query = query.Where(x => x.CustomerId == customerId);

                if (fromDate.HasValue)
                    query = query.Where(x => x.InvoiceDate >= fromDate);

                if (toDate.HasValue)
                    query = query.Where(x => x.InvoiceDate <= toDate);

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(x =>
                        x.InvoiceNo.Contains(search) ||
                        x.JobNo.Contains(search) ||
                        x.CustomerName.Contains(search));
                }

                int totalRecords = await query.CountAsync();

                var data = await query
                    .OrderByDescending(x => x.InvoiceNo)
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
                _logs.Write("SalesInvoice", "GetInvoices",
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

        public async Task<dynamic> GetInvoiceById(int invoiceId)
        {
            try
            {


                var invoice_check = await _context.SalesInvoices
                                             .FirstOrDefaultAsync(c => c.InvoiceId == invoiceId);

                if (invoice_check == null)
                {
                    return new
                    {
                        errorCode = 404,
                        errorMessage = "Invoice not found"
                    };
                }


                var invoice =
                    await (
                        from inv in _context.SalesInvoices

                            // CUSTOMER
                        join c in _context.Customers
                            on inv.CustomerId equals c.Id

                        // REGION
                        join r in _context.Regions
                            on c.RegionId equals r.Id into regionJoin
                        from r in regionJoin.DefaultIfEmpty()

                            // COUNTRY
                        join co in _context.Countries
                            on c.CountryId equals co.Id into countryJoin
                        from co in countryJoin.DefaultIfEmpty()

                            // JOB
                        join jm in _context.JobImportMasters
                            on inv.JobId equals jm.Id

                        // POL
                        join pol in _context.Pols
                            on jm.PolId equals pol.Id into polJoin
                        from pol in polJoin.DefaultIfEmpty()

                            // POD
                        join pod in _context.Pods
                            on jm.PodId equals pod.Id into podJoin
                        from pod in podJoin.DefaultIfEmpty()

                            // SHIPMENT MODE
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

                            // CUSTOMER
                            CustomerName = c.CustomerName,
                            CustomerNameAr = c.CustomerNameAr,
                            CustomerAddress = c.Address,

                            // REGION & COUNTRY
                            RegionName = r.Name,
                            CountryName = co.Name,

                            CustomerVat = c.VatId,

                            // BANK DETAILS
                            BankName = c.BankName,
                            AccountName = c.AccountNumber,
                            AccountNumber = c.AccountNumber,
                            IBAN = c.Iban,
                            SwiftCode = c.SwiftCode,

                            // JOB
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

                var details =
                    await (
                        from d in _context.SalesInvoiceDetails

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
                            .FirstOrDefaultAsync(x => x.ReferenceId == _session.ReferenceId);

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
                        user.Vatnumber
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

        public async Task<dynamic> EditSalesInvoice(int invoiceId)
        {
            try
            {
                SqlParameter[] parameter =
                {
                    new SqlParameter { ParameterName = "@InvoiceId", Value = invoiceId },
                };

                var result = await _db.Fetch(
                    "sp_EditSalesInvoice",
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
                    "Sales Invoice",
                    "EditSalesInvoice",
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

        public async Task<dynamic> SavePaymentReceived(PaymentReceived model)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    var invoice = await _context.SalesInvoices
                        .FirstOrDefaultAsync(x => x.InvoiceId == model.InvoiceId);

                    if (invoice == null)
                    {
                        return new
                        {
                            errorCode = 404,
                            data = "Invoice not found"
                        };
                    }

                    decimal totalReceived = await _context.PaymentReceiveds
                        .Where(x => x.InvoiceId == model.InvoiceId)
                        .SumAsync(x => (decimal?)x.Amount) ?? 0;

                    // Edit mode adjustment
                    if (model.Id > 0)
                    {
                        var existingPayment = await _context.PaymentReceiveds
                            .FirstOrDefaultAsync(x => x.Id == model.Id);

                        if (existingPayment != null)
                        {
                            totalReceived -= existingPayment.Amount;
                        }
                    }

                    // Over Payment Validation
                    if ((totalReceived + model.Amount) > invoice.GrandTotal)
                    {
                        return new
                        {
                            errorCode = 201,
                            data = $"Payment exceeds invoice amount. Remaining balance is {(invoice.GrandTotal - totalReceived):N2}"
                        };
                    }

                    PaymentReceived payment;

                    if (model.Id == 0)
                    {
                        var maxId = await _context.PaymentReceiveds
                            .MaxAsync(x => (int?)x.Id) ?? 0;

                        payment = new PaymentReceived
                        {
                            Id = maxId + 1,
                            InvoiceId = model.InvoiceId,
                            Rvnumber = model.Rvnumber,
                            PaymentDate = model.PaymentDate,
                            Amount = model.Amount,
                            PaymentMode = model.PaymentMode,
                            BlNo = model.BlNo,
                            IsActive = true,
                            CreatedBy = _session.LoginId,
                            CreatedOn = DateTime.Now
                        };

                        _context.PaymentReceiveds.Add(payment);
                    }
                    else
                    {
                        payment = await _context.PaymentReceiveds
                            .FirstOrDefaultAsync(x => x.Id == model.Id);

                        if (payment == null)
                        {
                            return new
                            {
                                errorCode = 404,
                                data = "Payment record not found"
                            };
                        }

                        payment.Rvnumber = model.Rvnumber;
                        payment.PaymentDate = model.PaymentDate;
                        payment.Amount = model.Amount;
                        payment.PaymentMode = model.PaymentMode;
                        payment.BlNo = model.BlNo;
                        payment.ModifiedBy = _session.LoginId;
                        payment.ModifiedOn = DateTime.Now;
                    }

                    await _context.SaveChangesAsync();

                    // Recalculate Total Received
                    totalReceived = await _context.PaymentReceiveds
                        .Where(x => x.InvoiceId == model.InvoiceId)
                        .SumAsync(x => (decimal?)x.Amount) ?? 0;

                    // Update Invoice Values
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
                        "SalesInvoice",
                        "SavePaymentReceived",
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
                "sp_GetSalesReport",
                parameters,
                CommandType.StoredProcedure
            );

            var table = result.tables[0];

            int totalRecords = table.Rows.Count;

            // 🔥 SAFE CONVERSION (IMPORTANT FIX)
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

        public async Task<dynamic> GetCustomerStatement(IList<QueryFilters> filters)
        {
            int draw = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "draw")?.filterValue ?? "0");
            int start = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "start")?.filterValue ?? "0");
            int length = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "length")?.filterValue ?? "10");

            string customerId = filters.FirstOrDefault(x => x.fieldName == "customerId")?.filterValue;
            string fromDate = filters.FirstOrDefault(x => x.fieldName == "fromDate")?.filterValue;
            string toDate = filters.FirstOrDefault(x => x.fieldName == "toDate")?.filterValue;

            string orderColumn = filters.FirstOrDefault(x => x.fieldName == "orderColumn")?.filterValue;
            string orderDir = filters.FirstOrDefault(x => x.fieldName == "orderDir")?.filterValue;

            SqlParameter[] parameters =
            {
                new SqlParameter("@CustomerId", string.IsNullOrEmpty(customerId) ? DBNull.Value : customerId),
                new SqlParameter("@FromDate", string.IsNullOrEmpty(fromDate) ? DBNull.Value : fromDate),
                new SqlParameter("@ToDate", string.IsNullOrEmpty(toDate) ? DBNull.Value : toDate),
            };

            var result = await _db.Fetch(
                "sp_GetCustomerSalesSOA",
                parameters,
                CommandType.StoredProcedure
            );

            var table = result.tables[0];

            int totalRecords = table.Rows.Count;

            // 🔥 SAFE CONVERSION (IMPORTANT FIX)
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

        public async Task<dynamic> GetGovernmentTaxReport(IList<QueryFilters> filters)
        {
            try
            {
                string fromDate = filters.FirstOrDefault(x => x.fieldName == "FromDate")?.filterValue;
                string toDate = filters.FirstOrDefault(x => x.fieldName == "ToDate")?.filterValue;

                SqlParameter[] parameters =
                {
                    new SqlParameter("@FromDate", Convert.ToDateTime(fromDate)),
                    new SqlParameter("@ToDate", Convert.ToDateTime(toDate)),
                    new SqlParameter("@ReferenceId", _session.ReferenceId)
                };

                var result = await _db.Fetch(
                    "sp_GetGovernmentTaxReportSummary",
                    parameters,
                    CommandType.StoredProcedure
                );

                if (result.tables.Count == 0 || result.tables[0].Rows.Count == 0)
                {
                    return new
                    {
                        errorCode = 404,
                        errorMessage = "No record found."
                    };
                }

                var row = result.tables[0].Rows[0];

                return new
                {
                    FromDate = row["FromDate"],
                    ToDate = row["ToDate"],
                    TotalSalesVAT = row["TotalSalesVAT"],
                    TotalPurchaseVAT = row["TotalPurchaseVAT"],
                    GovernmentTax = row["GovernmentTax"],
                    errorCode = 200
                };
            }
            catch (Exception ex)
            {
                _logs.Write(
                    "SalesInvoice",
                    "GetGovernmentTaxReport",
                    ex.InnerException?.Message ?? ex.Message
                );

                return new
                {
                    errorCode = 500,
                    errorMessage = ex.Message
                };
            }
        }

        public async Task<byte[]> DownloadGovernmentTaxPdf(DateTime fromDate, DateTime toDate)
        {
            SqlParameter[] parameters =
            {
                new SqlParameter("@FromDate", fromDate),
                new SqlParameter("@ToDate", toDate),
                new SqlParameter("@ReferenceId", _session.ReferenceId)
            };

            var result = await _db.Fetch(
                "sp_GetGovernmentTaxReport",
                parameters,
                CommandType.StoredProcedure);

            if (result.tables.Count < 3)
            {
                throw new Exception("Invalid data returned from database.");
            }

            var salesTable = result.tables[0];
            var purchaseTable = result.tables[1];
            var summaryTable = result.tables[2];

            if (summaryTable.Rows.Count == 0)
            {
                throw new Exception("No data found.");
            }

            // Dynamic Sales Rows Build
            var salesRowsHtml = new StringBuilder();
            foreach (DataRow r in salesTable.Rows)
            {
                string creditNote = DataHelper.stringParse(r["CreditNoteNo"]);
                string cnClass = creditNote != "-" ? "text-red" : "";
                salesRowsHtml.Append($@"
                    <tr>
                        <td>{r["InvoiceNo"]}</td>
                        <td>{r["InvoiceDate"]}</td>
                        <td style='text-align:left;'>{r["CustomerName"]}</td>
                        <td>{Convert.ToDecimal(r["VatAmount"]):N2}</td>
                        <td>{Convert.ToDecimal(r["TotalInvoiceAmount"]):N2}</td>
                        <td class='{cnClass}'>{creditNote}</td>
                    </tr>");
            }

            // Dynamic Purchase Rows Build
            var purchaseRowsHtml = new StringBuilder();
            foreach (DataRow r in purchaseTable.Rows)
            {
                string creditNote = DataHelper.stringParse(r["CreditNoteNo"]);
                string cnClass = creditNote != "-" ? "text-red" : "";
                purchaseRowsHtml.Append($@"
                    <tr>
                        <td>{r["InvoiceNo"]}</td>
                        <td>{r["InvoiceDate"]}</td>
                        <td style='text-align:left;'>{r["ServiceProviderName"]}</td>
                        <td>{Convert.ToDecimal(r["VatAmount"]):N2}</td>
                        <td>{Convert.ToDecimal(r["TotalInvoiceAmount"]):N2}</td>
                        <td class='{cnClass}'>{creditNote}</td>
                    </tr>");
            }

            // Summary Calculations
            var summaryRow = summaryTable.Rows[0];
            decimal totalSalesVat = Convert.ToDecimal(summaryRow["TotalSalesVAT"]);
            decimal totalPurchaseVat = Convert.ToDecimal(summaryRow["TotalPurchaseVAT"]);
            decimal governmentTax = Convert.ToDecimal(summaryRow["GovernmentTax"]);

            string html = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <style>
                        body {{
                            font-family: Arial, sans-serif;
                            margin: 20px;
                            color: #000;
                        }}

                        .title {{
                            text-align: center;
                            font-size: 24px;
                            font-weight: 900;
                            letter-spacing: 0.5px;
                            color: #0c1c38;
                        }}

                        .period {{
                            text-align: center;
                            font-weight: bold;
                            margin-top: 5px;
                            margin-bottom: 20px;
                            font-size: 14px;
                        }}

                        table {{
                            width: 100%;
                            border-collapse: collapse;
                            margin-bottom: 20px;
                            text-align: center;
                            font-size: 13px;
                        }}

                        table, th, td {{
                            border: 1px solid #000;
                        }}

                        th, td {{
                            padding: 8px 5px;
                        }}

                        /* Sales Table Style */
                        .sales-table th {{
                            background-color: #dce6f1;
                        }}
                        .sales-header {{
                            background-color: #e9eef4;
                            font-size: 16px;
                            font-weight: bold;
                        }}
                        .sales-total {{
                            background-color: #e9eef4;
                            font-weight: bold;
                        }}

                        /* Purchase Table Style */
                        .purchase-table th {{
                            background-color: #d8e4bc;
                        }}
                        .purchase-header {{
                            background-color: #ebf1de;
                            font-size: 16px;
                            font-weight: bold;
                        }}
                        .purchase-total {{
                            background-color: #ebf1de;
                            font-weight: bold;
                        }}

                        /* Summary Table Style */
                        .summary-table th {{
                            background-color: #fff2c2;
                        }}
                        .summary-header {{
                            background-color: #fef6d8;
                            font-size: 16px;
                            font-weight: bold;
                        }}
                        .summary-total {{
                            background-color: #fff2c2;
                            font-weight: bold;
                        }}

                        .text-red {{
                            color: #c00000;
                            font-weight: bold;
                        }}

                        .formula-section {{
                            text-align: center;
                            margin-top: 15px;
                            font-size: 14px;
                            font-weight: bold;
                        }}

                        .formula-calc {{
                            margin-top: 8px;
                            font-size: 15px;
                        }}

                        .amount-box-container {{
                            display: flex;
                            justify-content: center;
                            margin-top: 20px;
                        }}

                        .amount-box {{
                            border: 1.5px dashed #7f7f7f;
                            background-color: #f2f2f2;
                            padding: 10px 20px;
                            border-radius: 6px;
                            display: inline-block;
                            font-size: 16px;
                            font-weight: bold;
                        }}
                    </style>
                </head>
                <body>

                    <div class='title'>GOVERNMENT TAX REPORT</div>
                    <div class='period'>Period : {fromDate:dd/MM/yyyy} - {toDate:dd/MM/yyyy}</div>

                    <!-- SALES INVOICES TABLE -->
                    <table class='sales-table'>
                        <thead>
                            <tr class='sales-header'>
                                <th colspan='6'>SALES INVOICES</th>
                            </tr>
                            <tr>
                                <th style='width: 15%;'>Invoice No</th>
                                <th style='width: 13%;'>Invoice Date</th>
                                <th style='width: 27%;'>Customer Name</th>
                                <th style='width: 13%;'>VAT Amount</th>
                                <th style='width: 17%;'>Total Invoice Amount</th>
                                <th style='width: 15%;'>Credit Note No<br>(if Cancelled)</th>
                            </tr>
                        </thead>
                        <tbody>
                            {salesRowsHtml}
                            <tr class='sales-total'>
                                <td colspan='4' style='text-align:right; font-weight:bold;'>Total Sales VAT</td>
                                <td colspan='2' style='text-align:center; font-weight:bold;'>{totalSalesVat:N2}</td>
                            </tr>
                        </tbody>
                    </table>

                    <!-- PURCHASE INVOICES TABLE -->
                    <table class='purchase-table'>
                        <thead>
                            <tr class='purchase-header'>
                                <th colspan='6'>PURCHASE INVOICES</th>
                            </tr>
                            <tr>
                                <th style='width: 15%;'>Invoice No</th>
                                <th style='width: 13%;'>Invoice Date</th>
                                <th style='width: 27%;'>Service Provider Name</th>
                                <th style='width: 13%;'>VAT Amount</th>
                                <th style='width: 17%;'>Total Invoice Amount</th>
                                <th style='width: 15%;'>Credit Note No<br>(if Cancelled)</th>
                            </tr>
                        </thead>
                        <tbody>
                            {purchaseRowsHtml}
                            <tr class='purchase-total'>
                                <td colspan='4' style='text-align:right; font-weight:bold;'>Total Purchase VAT</td>
                                <td colspan='2' style='text-align:center; font-weight:bold;'>{totalPurchaseVat:N2}</td>
                            </tr>
                        </tbody>
                    </table>

                    <!-- SUMMARY TABLE -->
                    <table class='summary-table'>
                        <thead>
                            <tr class='summary-header'>
                                <th colspan='2'>GOVERNMENT TAX SUMMARY</th>
                            </tr>
                            <tr>
                                <th style='width: 65%;'>Description</th>
                                <th style='width: 35%;'>Amount</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr>
                                <td style='text-align:left;'>Total Sales VAT</td>
                                <td>{totalSalesVat:N2}</td>
                            </tr>
                            <tr>
                                <td style='text-align:left;'>Total Purchase VAT</td>
                                <td>{totalPurchaseVat:N2}</td>
                            </tr>
                            <tr class='summary-total'>
                                <td style='text-align:left;'>Government Tax (Sales VAT - Purchase VAT)</td>
                                <td>{governmentTax:N2}</td>
                            </tr>
                        </tbody>
                    </table>

                    <!-- FORMULA & FINAL AMOUNT -->
                    <div class='formula-section'>
                        <div>Government Tax = All Sales Invoice VAT Amount - All Purchase Invoice VAT Amount</div>
                        <div class='formula-calc'>
                            <span style='color:#1f497d;'>{totalSalesVat:N2}</span> - 
                            <span style='color:#c00000;'>{totalPurchaseVat:N2}</span> = 
                            <span style='color:#1f497d;'>{governmentTax:N2}</span>
                        </div>
                    </div>

                    <div class='amount-box-container' style='text-align:center;'>
                        <div class='amount-box'>
                            GOVERNMENT TAX AMOUNT = <span style='color:#002060;'>{governmentTax:N2}</span>
                        </div>
                    </div>

                </body>
                </html>";

            return GeneratePdfFromHtml(html);
        }

        public async Task<byte[]> DownloadGovernmentTaxPdf_Bk(DateTime fromDate, DateTime toDate)
        {
            SqlParameter[] parameters =
            {
                new SqlParameter("@FromDate", fromDate),
                new SqlParameter("@ToDate", toDate)
            };

            var result = await _db.Fetch(
                "sp_GetGovernmentTaxReport",
                parameters,
                CommandType.StoredProcedure);

            if (result.tables.Count == 0 ||
                result.tables[0].Rows.Count == 0)
            {
                throw new Exception("No data found.");
            }

            var row = result.tables[0].Rows[0];

            decimal totalSalesVat =
                Convert.ToDecimal(row["TotalSalesVAT"]);

            decimal totalPurchaseVat =
                Convert.ToDecimal(row["TotalPurchaseVAT"]);

            decimal governmentTax =
                Convert.ToDecimal(row["GovernmentTax"]);

            string html = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <style>
                    body {{
                        font-family: Arial;
                        margin:40px;
                    }}

                    .title {{
                        text-align:center;
                        font-size:22px;
                        font-weight:bold;
                        margin-bottom:20px;
                    }}

                    .period {{
                        text-align:center;
                        margin-bottom:30px;
                    }}

                    table {{
                        width:100%;
                        border-collapse:collapse;
                    }}

                    table, th, td {{
                        border:1px solid #000;
                    }}

                    th, td {{
                        padding:10px;
                    }}

                    .formula {{
                        margin-top:30px;
                        font-size:16px;
                        font-weight:bold;
                    }}

                    .amount {{
                        margin-top:20px;
                        font-size:18px;
                        font-weight:bold;
                    }}
                </style>
            </head>
            <body>

                <div class='title'>
                    GOVERNMENT TAX REPORT
                </div>

                <div class='period'>
                    Period :
                    {fromDate:dd/MM/yyyy}
                    -
                    {toDate:dd/MM/yyyy}
                </div>

                <table>
                    <thead>
                        <tr>
                            <th>Description</th>
                            <th>Amount</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr>
                            <td>Total Sales VAT</td>
                            <td>{totalSalesVat:N2}</td>
                        </tr>
                        <tr>
                            <td>Total Purchase VAT</td>
                            <td>{totalPurchaseVat:N2}</td>
                        </tr>
                        <tr>
                            <td><strong>Government Tax</strong></td>
                            <td><strong>{governmentTax:N2}</strong></td>
                        </tr>
                    </tbody>
                </table>

                <div class='formula'>
                    Government Tax = All Sales Invoice VAT Amount
                    - All Purchase Invoice VAT Amount
                    <br /><br />

                    GOVT. TAX =
                    {totalSalesVat:N2}
                    -
                    {totalPurchaseVat:N2}
                    =
                    {governmentTax:N2}/-
                </div>

                <div class='amount'>
                    GOVT. TAX AMOUNT =
                    {governmentTax:N2}
                </div>

            </body>
            </html>";

            return GeneratePdfFromHtml(html);

        }

        public async Task<bool> IsInvoiceEditable(int invoiceId)
        {
            return !await _context.SalesInvoices
                .AsNoTracking()
                .AnyAsync(x => x.InvoiceId == invoiceId && (x.PaidAmount ?? 0) > 0);
        }

        public async Task<dynamic> GetNextCreditNoteNumber()
        {
            var year = DateTime.Now.Year;

            string prefix = $"CN-{year}-";

            var lastCreditNoteNo = await _context.SalesCreditNotes
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
                    var invoice = await _context.SalesInvoices
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

                    var maxId = await _context.SalesCreditNotes.MaxAsync(x => (int?)x.CreditNoteId) ?? 0;

                    SalesCreditNote creditNote;

                    creditNote = new SalesCreditNote
                    {
                        CreditNoteId = maxId + 1,
                        CreditNoteNo = creditNoteNo,
                        InvoiceId = invoice.InvoiceId,
                        InvoiceNo = invoice.InvoiceNo,
                        InvoiceDate = invoice.InvoiceDate,
                        CustomerId = invoice.CustomerId,
                        NetAmount = invoice.NetAmount,
                        VatAmount = invoice.VatAmount,
                        GrandTotal = invoice.GrandTotal,
                        CreditNoteDate = DateTime.Now,
                        IsActive = true,
                        ReferenceId = _session.ReferenceId,
                        CreatedBy = _session.LoginId,
                        CreatedOn = DateTime.Now
                    };

                    _context.SalesCreditNotes.Add(creditNote);

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
                        "SalesInvoice",
                        "CancelInvoice",
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
                var data = await (from cn in _context.SalesCreditNotes

                                  join inv in _context.SalesInvoices
                                      on cn.InvoiceId equals inv.InvoiceId

                                  join cust in _context.Customers
                                      on inv.CustomerId equals cust.Id

                                  where cn.InvoiceId == invoiceId

                                  select new
                                  {
                                      cn.CreditNoteId,
                                      cn.CreditNoteNo,
                                      cn.InvoiceNo,
                                      CustomerName = cust.CustomerName,
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
                        data.CustomerName,
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
                    "SalesInvoice",
                    "GetCreditNoteSummary",
                    ex.InnerException?.Message ?? ex.Message
                );

                return new
                {
                    errorCode = 999,
                    data = ex.Message
                };
            }
        }

        public async Task<byte[]> DownloadGovernmentTaxExcel_seperate(DateTime fromDate, DateTime toDate)
        {
            SqlParameter[] parameters =
            {
                new SqlParameter("@FromDate", fromDate),
                new SqlParameter("@ToDate", toDate),
                new SqlParameter("@ReferenceId", (object)_session.ReferenceId ?? DBNull.Value)
            };

            var result = await _db.Fetch(
                "sp_GetGovernmentTaxReport",
                parameters,
                CommandType.StoredProcedure);

            if (result.tables.Count < 3)
            {
                throw new Exception("Invalid data returned from database.");
            }

            var salesTable = result.tables[0];
            var purchaseTable = result.tables[1];
            var summaryTable = result.tables[2];

            if (summaryTable.Rows.Count == 0)
            {
                throw new Exception("No data found in summary.");
            }

            // =========================================================
            // DIRECT VALUES FROM SQL STORED PROCEDURE (TABLE 2)
            // =========================================================
            var summaryRow = summaryTable.Rows[0];

            decimal totalSalesVat = summaryRow["TotalSalesVAT"] != DBNull.Value ? Convert.ToDecimal(summaryRow["TotalSalesVAT"]) : 0m;
            decimal totalPurchaseVat = summaryRow["TotalPurchaseVAT"] != DBNull.Value ? Convert.ToDecimal(summaryRow["TotalPurchaseVAT"]) : 0m;
            decimal netGovernmentTax = summaryRow["GovernmentTax"] != DBNull.Value ? Convert.ToDecimal(summaryRow["GovernmentTax"]) : 0m;


            using (var workbook = new XLWorkbook())
            {
                var navyHeaderColor = XLColor.FromHtml("#1F4E78");
                var zebraColor = XLColor.FromHtml("#F9FAFB");
                var totalRowColor = XLColor.FromHtml("#EAEEF3");

                // ==========================================
                // 1. TAX SUMMARY SHEET (Matches Image Layout)
                // ==========================================
                var wsSummary = workbook.Worksheets.Add("Tax Summary");

                // Title
                wsSummary.Cell("A1").Value = "GOVERNMENT TAX REPORT";
                wsSummary.Cell("A1").Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(navyHeaderColor);

                // Subtitle Date Range
                wsSummary.Cell("A2").Value = $"Report Period: {fromDate:dd/MM/yyyy} to {toDate:dd/MM/yyyy}";
                wsSummary.Cell("A2").Style.Font.SetItalic().Font.SetFontSize(11).Font.SetFontColor(XLColor.FromHtml("#595959"));

                // KPI Box 1: TOTAL SALES VAT (Direct from SP)
                wsSummary.Range("A4:B4").Merge().Value = "TOTAL SALES VAT";
                wsSummary.Range("A4:B4").Style.Font.SetBold().Font.SetFontSize(9).Font.SetFontColor(XLColor.FromHtml("#595959")).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                wsSummary.Range("A5:B5").Merge().SetValue(totalSalesVat);
                wsSummary.Range("A5:B5").Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(navyHeaderColor).NumberFormat.Format = "#,##0.00";
                wsSummary.Range("A5:B5").Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                wsSummary.Range("A4:B5").Style.Fill.SetBackgroundColor(XLColor.FromHtml("#E8F1F5")).Border.SetOutsideBorder(XLBorderStyleValues.Thin);

                // KPI Box 2: TOTAL PURCHASE VAT (Direct from SP)
                wsSummary.Range("C4:D4").Merge().Value = "TOTAL PURCHASE VAT";
                wsSummary.Range("C4:D4").Style.Font.SetBold().Font.SetFontSize(9).Font.SetFontColor(XLColor.FromHtml("#595959")).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                wsSummary.Range("C5:D5").Merge().SetValue(totalPurchaseVat);
                wsSummary.Range("C5:D5").Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(XLColor.FromHtml("#C00000")).NumberFormat.Format = "#,##0.00";
                wsSummary.Range("C5:D5").Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                wsSummary.Range("C4:D5").Style.Fill.SetBackgroundColor(XLColor.FromHtml("#FDE9D9")).Border.SetOutsideBorder(XLBorderStyleValues.Thin);

                // KPI Box 3: NET GOVERNMENT TAX (Direct from SP)
                wsSummary.Range("E4:F4").Merge().Value = "NET GOVERNMENT TAX";
                wsSummary.Range("E4:F4").Style.Font.SetBold().Font.SetFontSize(9).Font.SetFontColor(XLColor.FromHtml("#595959")).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                wsSummary.Range("E5:F5").Merge().SetValue(netGovernmentTax);
                wsSummary.Range("E5:F5").Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(XLColor.FromHtml("#276A3C")).NumberFormat.Format = "#,##0.00";
                wsSummary.Range("E5:F5").Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                wsSummary.Range("E4:F5").Style.Fill.SetBackgroundColor(XLColor.FromHtml("#E2EFDA")).Border.SetOutsideBorder(XLBorderStyleValues.Thin);

                // ==========================================
                // 2. SALES INVOICES SHEET
                // ==========================================
                var wsSales = workbook.Worksheets.Add("Sales Invoices");
                wsSales.Cell("A1").Value = "SALES INVOICES TAX REPORT";
                wsSales.Cell("A1").Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(navyHeaderColor);
                wsSales.Cell("A2").Value = $"Filtered Period: {fromDate:dd/MM/yyyy} to {toDate:dd/MM/yyyy}";
                wsSales.Cell("A2").Style.Font.SetItalic().Font.SetFontSize(11).Font.SetFontColor(XLColor.FromHtml("#595959"));

                string[] salesHeaders = { "Invoice No", "Invoice Date", "Customer Name", "VAT Amount", "Total Invoice Amount", "Credit Note No" };
                for (int i = 0; i < salesHeaders.Length; i++)
                {
                    var cell = wsSales.Cell(4, i + 1);
                    cell.Value = salesHeaders[i];
                    cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
                    cell.Style.Fill.SetBackgroundColor(navyHeaderColor);
                    cell.Style.Alignment.SetHorizontal(i <= 1 || i == 5 ? XLAlignmentHorizontalValues.Center : (i >= 3 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left));
                }

                int sRow = 5;
                foreach (DataRow dr in salesTable.Rows)
                {
                    wsSales.Cell(sRow, 1).SetValue(dr["InvoiceNo"]?.ToString()).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    wsSales.Cell(sRow, 2).SetValue(dr["InvoiceDate"]?.ToString()).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    wsSales.Cell(sRow, 3).SetValue(dr["CustomerName"]?.ToString());
                    wsSales.Cell(sRow, 4).SetValue(dr["VatAmount"] != DBNull.Value ? Convert.ToDecimal(dr["VatAmount"]) : 0m).Style.NumberFormat.Format = "#,##0.00";
                    wsSales.Cell(sRow, 5).SetValue(dr["TotalInvoiceAmount"] != DBNull.Value ? Convert.ToDecimal(dr["TotalInvoiceAmount"]) : 0m).Style.NumberFormat.Format = "#,##0.00";
                    wsSales.Cell(sRow, 6).SetValue(dr["CreditNoteNo"]?.ToString()).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                    if (sRow % 2 != 0)
                        wsSales.Range(sRow, 1, sRow, 6).Style.Fill.SetBackgroundColor(zebraColor);

                    wsSales.Range(sRow, 1, sRow, 6).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Thin);
                    sRow++;
                }

                // Sales Total Row (Set Direct SP Total)
                wsSales.Cell(sRow, 1).Value = "Total";
                wsSales.Cell(sRow, 1).Style.Font.SetBold();
                wsSales.Cell(sRow, 4).SetValue(totalSalesVat).Style.Font.SetBold().NumberFormat.Format = "#,##0.00";

                wsSales.Range(sRow, 1, sRow, 6).Style.Fill.SetBackgroundColor(totalRowColor);
                wsSales.Range(sRow, 1, sRow, 6).Style.Border.SetTopBorder(XLBorderStyleValues.Thin).Border.SetBottomBorder(XLBorderStyleValues.Double);

                // ==========================================
                // 3. PURCHASE INVOICES SHEET
                // ==========================================
                var wsPurchase = workbook.Worksheets.Add("Purchase Invoices");
                wsPurchase.Cell("A1").Value = "PURCHASE INVOICES TAX REPORT";
                wsPurchase.Cell("A1").Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(navyHeaderColor);
                wsPurchase.Cell("A2").Value = $"Filtered Period: {fromDate:dd/MM/yyyy} to {toDate:dd/MM/yyyy}";
                wsPurchase.Cell("A2").Style.Font.SetItalic().Font.SetFontSize(11).Font.SetFontColor(XLColor.FromHtml("#595959"));

                string[] purchaseHeaders = { "Invoice No", "Invoice Date", "Service Provider Name", "VAT Amount", "Total Invoice Amount", "Credit Note No" };
                for (int i = 0; i < purchaseHeaders.Length; i++)
                {
                    var cell = wsPurchase.Cell(4, i + 1);
                    cell.Value = purchaseHeaders[i];
                    cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
                    cell.Style.Fill.SetBackgroundColor(navyHeaderColor);
                    cell.Style.Alignment.SetHorizontal(i <= 1 || i == 5 ? XLAlignmentHorizontalValues.Center : (i >= 3 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left));
                }

                int pRow = 5;
                foreach (DataRow dr in purchaseTable.Rows)
                {
                    wsPurchase.Cell(pRow, 1).SetValue(dr["InvoiceNo"]?.ToString()).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    wsPurchase.Cell(pRow, 2).SetValue(dr["InvoiceDate"]?.ToString()).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    wsPurchase.Cell(pRow, 3).SetValue(dr["ServiceProviderName"]?.ToString());
                    wsPurchase.Cell(pRow, 4).SetValue(dr["VatAmount"] != DBNull.Value ? Convert.ToDecimal(dr["VatAmount"]) : 0m).Style.NumberFormat.Format = "#,##0.00";
                    wsPurchase.Cell(pRow, 5).SetValue(dr["TotalInvoiceAmount"] != DBNull.Value ? Convert.ToDecimal(dr["TotalInvoiceAmount"]) : 0m).Style.NumberFormat.Format = "#,##0.00";
                    wsPurchase.Cell(pRow, 6).SetValue(dr["CreditNoteNo"]?.ToString()).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                    if (pRow % 2 != 0)
                        wsPurchase.Range(pRow, 1, pRow, 6).Style.Fill.SetBackgroundColor(zebraColor);

                    wsPurchase.Range(pRow, 1, pRow, 6).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Thin);
                    pRow++;
                }

                // Purchase Total Row (Set Direct SP Total)
                wsPurchase.Cell(pRow, 1).Value = "Total";
                wsPurchase.Cell(pRow, 1).Style.Font.SetBold();
                wsPurchase.Cell(pRow, 4).SetValue(totalPurchaseVat).Style.Font.SetBold().NumberFormat.Format = "#,##0.00";

                wsPurchase.Range(pRow, 1, pRow, 6).Style.Fill.SetBackgroundColor(totalRowColor);
                wsPurchase.Range(pRow, 1, pRow, 6).Style.Border.SetTopBorder(XLBorderStyleValues.Thin).Border.SetBottomBorder(XLBorderStyleValues.Double);

                // Auto Column Width Adjustment
                foreach (var sheet in workbook.Worksheets)
                {
                    sheet.Columns().AdjustToContents();
                }

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        public async Task<byte[]> DownloadGovernmentTaxExcel(DateTime fromDate, DateTime toDate)
        {
            SqlParameter[] parameters =
            {
                new SqlParameter("@FromDate", fromDate),
                new SqlParameter("@ToDate", toDate),
                new SqlParameter("@ReferenceId", (object)_session.ReferenceId ?? DBNull.Value)
            };

            var result = await _db.Fetch(
                "sp_GetGovernmentTaxReport",
                parameters,
                CommandType.StoredProcedure);

            if (result.tables.Count < 3)
            {
                throw new Exception("Invalid data returned from database.");
            }

            var salesTable = result.tables[0];
            var purchaseTable = result.tables[1];
            var summaryTable = result.tables[2];

            if (summaryTable.Rows.Count == 0)
            {
                throw new Exception("No data found in summary.");
            }

            var summaryRow = summaryTable.Rows[0];
            decimal totalSalesVat = summaryRow["TotalSalesVAT"] != DBNull.Value ? Convert.ToDecimal(summaryRow["TotalSalesVAT"]) : 0m;
            decimal totalPurchaseVat = summaryRow["TotalPurchaseVAT"] != DBNull.Value ? Convert.ToDecimal(summaryRow["TotalPurchaseVAT"]) : 0m;
            decimal netGovernmentTax = summaryRow["GovernmentTax"] != DBNull.Value ? Convert.ToDecimal(summaryRow["GovernmentTax"]) : 0m;

            using (var workbook = new XLWorkbook())
            {
                var navyHeaderColor = XLColor.FromHtml("#1F4E78");
                var zebraColor = XLColor.FromHtml("#F9FAFB");
                var totalRowColor = XLColor.FromHtml("#EAEEF3");

                // Sirf 1 Sheet banayein
                var ws = workbook.Worksheets.Add("Tax Report");

                // ==========================================
                // 1. HEADER SECTION
                // ==========================================
                ws.Cell("A1").Value = "GOVERNMENT TAX REPORT";
                ws.Cell("A1").Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(navyHeaderColor);

                ws.Cell("A2").Value = $"Report Period: {fromDate:dd/MM/yyyy} to {toDate:dd/MM/yyyy}";
                ws.Cell("A2").Style.Font.SetItalic().Font.SetFontSize(11).Font.SetFontColor(XLColor.FromHtml("#595959"));

                // ==========================================
                // 2. KPI SUMMARY CARDS (Rows 4-5)
                // ==========================================
                // Total Sales VAT
                ws.Range("A4:B4").Merge().Value = "TOTAL SALES VAT";
                ws.Range("A4:B4").Style.Font.SetBold().Font.SetFontSize(9).Font.SetFontColor(XLColor.FromHtml("#595959")).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Range("A5:B5").Merge().SetValue(totalSalesVat);
                ws.Range("A5:B5").Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(navyHeaderColor).NumberFormat.Format = "#,##0.00";
                ws.Range("A5:B5").Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Range("A4:B5").Style.Fill.SetBackgroundColor(XLColor.FromHtml("#E8F1F5")).Border.SetOutsideBorder(XLBorderStyleValues.Thin);

                // Total Purchase VAT
                ws.Range("C4:D4").Merge().Value = "TOTAL PURCHASE VAT";
                ws.Range("C4:D4").Style.Font.SetBold().Font.SetFontSize(9).Font.SetFontColor(XLColor.FromHtml("#595959")).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Range("C5:D5").Merge().SetValue(totalPurchaseVat);
                ws.Range("C5:D5").Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(XLColor.FromHtml("#C00000")).NumberFormat.Format = "#,##0.00";
                ws.Range("C5:D5").Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Range("C4:D5").Style.Fill.SetBackgroundColor(XLColor.FromHtml("#FDE9D9")).Border.SetOutsideBorder(XLBorderStyleValues.Thin);

                // Net Government Tax
                ws.Range("E4:F4").Merge().Value = "NET GOVERNMENT TAX";
                ws.Range("E4:F4").Style.Font.SetBold().Font.SetFontSize(9).Font.SetFontColor(XLColor.FromHtml("#595959")).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Range("E5:F5").Merge().SetValue(netGovernmentTax);
                ws.Range("E5:F5").Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(XLColor.FromHtml("#276A3C")).NumberFormat.Format = "#,##0.00";
                ws.Range("E5:F5").Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Range("E4:F5").Style.Fill.SetBackgroundColor(XLColor.FromHtml("#E2EFDA")).Border.SetOutsideBorder(XLBorderStyleValues.Thin);

                // Dynamic Row Tracker (Starting point for Sales Section)
                int currentRow = 8;

                // ==========================================
                // 3. SALES INVOICES TABLE
                // ==========================================
                ws.Cell(currentRow, 1).Value = "SALES INVOICES";
                ws.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(13).Font.SetFontColor(navyHeaderColor);
                currentRow++;

                string[] salesHeaders = { "Invoice No", "Invoice Date", "Customer Name", "VAT Amount", "Total Invoice Amount", "Credit Note No" };
                for (int i = 0; i < salesHeaders.Length; i++)
                {
                    var cell = ws.Cell(currentRow, i + 1);
                    cell.Value = salesHeaders[i];
                    cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
                    cell.Style.Fill.SetBackgroundColor(navyHeaderColor);
                    cell.Style.Alignment.SetHorizontal(i <= 1 || i == 5 ? XLAlignmentHorizontalValues.Center : (i >= 3 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left));
                }
                currentRow++;

                foreach (DataRow dr in salesTable.Rows)
                {
                    ws.Cell(currentRow, 1).SetValue(dr["InvoiceNo"]?.ToString()).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    ws.Cell(currentRow, 2).SetValue(dr["InvoiceDate"]?.ToString()).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    ws.Cell(currentRow, 3).SetValue(dr["CustomerName"]?.ToString());
                    ws.Cell(currentRow, 4).SetValue(dr["VatAmount"] != DBNull.Value ? Convert.ToDecimal(dr["VatAmount"]) : 0m).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(currentRow, 5).SetValue(dr["TotalInvoiceAmount"] != DBNull.Value ? Convert.ToDecimal(dr["TotalInvoiceAmount"]) : 0m).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(currentRow, 6).SetValue(dr["CreditNoteNo"]?.ToString()).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                    if (currentRow % 2 != 0)
                        ws.Range(currentRow, 1, currentRow, 6).Style.Fill.SetBackgroundColor(zebraColor);

                    ws.Range(currentRow, 1, currentRow, 6).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Thin);
                    currentRow++;
                }

                // Sales Total Row
                ws.Cell(currentRow, 1).Value = "Total Sales VAT";
                ws.Cell(currentRow, 1).Style.Font.SetBold();
                ws.Cell(currentRow, 4).SetValue(totalSalesVat).Style.Font.SetBold().NumberFormat.Format = "#,##0.00";
                ws.Range(currentRow, 1, currentRow, 6).Style.Fill.SetBackgroundColor(totalRowColor);
                ws.Range(currentRow, 1, currentRow, 6).Style.Border.SetTopBorder(XLBorderStyleValues.Thin).Border.SetBottomBorder(XLBorderStyleValues.Double);

                // Gap between tables
                currentRow += 3;

                // ==========================================
                // 4. PURCHASE INVOICES TABLE
                // ==========================================
                ws.Cell(currentRow, 1).Value = "PURCHASE INVOICES";
                ws.Cell(currentRow, 1).Style.Font.SetBold().Font.SetFontSize(13).Font.SetFontColor(navyHeaderColor);
                currentRow++;

                string[] purchaseHeaders = { "Invoice No", "Invoice Date", "Service Provider Name", "VAT Amount", "Total Invoice Amount", "Credit Note No" };
                for (int i = 0; i < purchaseHeaders.Length; i++)
                {
                    var cell = ws.Cell(currentRow, i + 1);
                    cell.Value = purchaseHeaders[i];
                    cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
                    cell.Style.Fill.SetBackgroundColor(navyHeaderColor);
                    cell.Style.Alignment.SetHorizontal(i <= 1 || i == 5 ? XLAlignmentHorizontalValues.Center : (i >= 3 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left));
                }
                currentRow++;

                foreach (DataRow dr in purchaseTable.Rows)
                {
                    ws.Cell(currentRow, 1).SetValue(dr["InvoiceNo"]?.ToString()).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    ws.Cell(currentRow, 2).SetValue(dr["InvoiceDate"]?.ToString()).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    ws.Cell(currentRow, 3).SetValue(dr["ServiceProviderName"]?.ToString());
                    ws.Cell(currentRow, 4).SetValue(dr["VatAmount"] != DBNull.Value ? Convert.ToDecimal(dr["VatAmount"]) : 0m).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(currentRow, 5).SetValue(dr["TotalInvoiceAmount"] != DBNull.Value ? Convert.ToDecimal(dr["TotalInvoiceAmount"]) : 0m).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(currentRow, 6).SetValue(dr["CreditNoteNo"]?.ToString()).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                    if (currentRow % 2 != 0)
                        ws.Range(currentRow, 1, currentRow, 6).Style.Fill.SetBackgroundColor(zebraColor);

                    ws.Range(currentRow, 1, currentRow, 6).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Thin);
                    currentRow++;
                }

                // Purchase Total Row
                ws.Cell(currentRow, 1).Value = "Total Purchase VAT";
                ws.Cell(currentRow, 1).Style.Font.SetBold();
                ws.Cell(currentRow, 4).SetValue(totalPurchaseVat).Style.Font.SetBold().NumberFormat.Format = "#,##0.00";
                ws.Range(currentRow, 1, currentRow, 6).Style.Fill.SetBackgroundColor(totalRowColor);
                ws.Range(currentRow, 1, currentRow, 6).Style.Border.SetTopBorder(XLBorderStyleValues.Thin).Border.SetBottomBorder(XLBorderStyleValues.Double);

                // Auto Column Width
                ws.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        public async Task<long> GetNextSalesInvoiceSequenceNumber()
        {
            // follow existing project pattern: use Database.OpenConnectionAsync / CloseConnectionAsync
            await _context.Database.OpenConnectionAsync();
            try
            {
                using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT NEXT VALUE FOR dbo.SalesInvoiceSequence";

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

        public async Task<dynamic> GetNextSalesInvoiceNumber()
        {
            var currentYear = DateTime.Now.Year;
            var prefix = $"INV-{currentYear}-";

            // 1. Current year ke tamam valid invoice numbers memory me fetch karein
            var invoiceNumbers = await _context.SalesInvoices
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

            var invoiceNo = $"INV-{currentYear}-{nextNumber:D4}";

            return new
            {
                errorCode = 200,
                invoiceNo
            };
        }

        private async Task UpdateSequenceIfHigherAsync(int customSeqNum)
        {
            // Aap ke SQL Sequence ka name yahan 'SalesInvoiceSeq' ke bajaye apna Sequence Name likhein
            string sequenceName = "SalesInvoiceSeq";

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
