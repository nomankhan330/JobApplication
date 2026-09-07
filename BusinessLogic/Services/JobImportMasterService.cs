using Azure.Core;
using BusinessLogic.Helper;
using BusinessLogic.Interfaces;
using BusinessLogic.Models;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using static Dapper.SqlMapper;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using static System.Reflection.Metadata.BlobBuilder;

namespace BusinessLogic.Services
{
    public class JobImportMasterService : IJobImportMaster
    {
        private readonly AppDbContext _context;
        private readonly ISessionHelper _session;
        private readonly ILogs _logs;
        private readonly IDatabaseObject _db;

        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public JobImportMasterService(AppDbContext context, ISessionHelper session, ILogs logs, IDatabaseObject db)
        {
            _context = context;
            _session = session;
            _logs = logs;
            _db = db;
        }

        public async Task<dynamic> Save(JobImportMasterVM model)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    JobImportMaster entity;

                    // =========================
                    // 🔹 INSERT
                    // =========================
                    if (model.Id == 0)
                    {
                        int newId = (await _context.JobImportMasters
                            .MaxAsync(x => (int?)x.Id) ?? 0) + 1;

                        entity = new JobImportMaster
                        {
                            Id = newId,
                            ShipmentType = model.ShipmentType,
                            JobNumber = $"JBN-{newId:D6}",

                            CostCenterId = model.CostCenterId,
                            JobDate = model.JobDate,

                            ShipmentModeId = model.ShipmentModeId,
                            JobTypeId = model.JobTypeId,
                            OtherJobType = model.OtherJobType,

                            CustomerId = model.CustomerId,
                            Shipper = model.Shipper,
                            Consignee = model.Consignee,
                            Carrier = model.Carrier,

                            BookingDate = model.BookingDate,
                            PolId = model.PolId,
                            PodId = model.PodId,

                            Etd = model.Etd,
                            Eta = model.Eta,

                            BayanNo = model.BayanNo,
                            BlNo = model.BlNo,

                            ContainerQuantity = model.ContainerQuantity,
                            ContainerNo = model.ContainerNo,
                            ContainerTypeId = model.ContainerTypeId,

                            BayanPrintDate = model.BayanPrintDate,
                            ShipmentClearedDate = model.ShipmentClearedDate,
                            DeliveryDate = model.DeliveryDate,
                            ArrivalDate = model.ArrivalDate,

                            DeliveryCityId = model.DeliveryCityId,
                            BlTypeId = model.BlTypeId,

                            BookingNo = model.BookingNo,
                            SiReceivedDate = model.SiReceivedDate,
                            ManifestReceivedDate = model.ManifestReceivedDate,
                            SailDate = model.SailDate,
                            BlStatusId = model.BlStatusId,

                            ShipmentStatusId = model.ShipmentStatusId,

                            OurInvoiceNo = model.OurInvoiceNo,
                            CreditNoteNo = model.CreditNoteNo,
                            AddInvoiceNo = model.AddInvoiceNo,

                            Remarks = model.Remarks,

                            ReferenceId = _session.ReferenceId,
                            CreatedBy = _session.LoginId,
                            CreatedOn = DateTime.Now
                        };

                        _context.JobImportMasters.Add(entity);
                        await _context.SaveChangesAsync(); // 🔥 ID generated
                    }

                    // =========================
                    // 🔹 UPDATE
                    // =========================
                    else
                    {
                        entity = await _context.JobImportMasters.FirstOrDefaultAsync(x => x.Id == model.Id);

                        if (entity == null)
                            return new { errorCode = 404, message = "Record not found" };

                        entity.CostCenterId = model.CostCenterId;
                        entity.JobDate = model.JobDate;

                        entity.ShipmentModeId = model.ShipmentModeId;
                        entity.JobTypeId = model.JobTypeId;
                        entity.OtherJobType = model.OtherJobType;

                        entity.CustomerId = model.CustomerId;
                        entity.Shipper = model.Shipper;
                        entity.Consignee = model.Consignee;
                        entity.Carrier = model.Carrier;

                        entity.BookingDate = model.BookingDate;
                        entity.PolId = model.PolId;
                        entity.PodId = model.PodId;

                        entity.Etd = model.Etd;
                        entity.Eta = model.Eta;

                        entity.BayanNo = model.BayanNo;
                        entity.BlNo = model.BlNo;

                        entity.ContainerQuantity = model.ContainerQuantity;
                        entity.ContainerNo = model.ContainerNo;
                        entity.ContainerTypeId = model.ContainerTypeId;

                        entity.BayanPrintDate = model.BayanPrintDate;
                        entity.ShipmentClearedDate = model.ShipmentClearedDate;
                        entity.DeliveryDate = model.DeliveryDate;
                        entity.ArrivalDate = model.ArrivalDate;

                        entity.DeliveryCityId = model.DeliveryCityId;
                        entity.BlTypeId = model.BlTypeId;

                        entity.BookingNo = model.BookingNo;
                        entity.SiReceivedDate = model.SiReceivedDate;
                        entity.ManifestReceivedDate = model.ManifestReceivedDate;
                        entity.SailDate = model.SailDate;
                        entity.BlStatusId = model.BlStatusId;

                        entity.ShipmentStatusId = model.ShipmentStatusId;
                        entity.OurInvoiceNo = model.OurInvoiceNo;
                        entity.CreditNoteNo = model.CreditNoteNo;
                        entity.AddInvoiceNo = model.AddInvoiceNo;

                        entity.Remarks = model.Remarks;

                        entity.ReferenceId = _session.ReferenceId;
                        entity.ModifiedBy = _session.LoginId;
                        entity.ModifiedOn = DateTime.Now;
                    }

                    // 🔥 IMPORTANT: correct ID usage
                    int jobId = entity.Id;
                    string jobNumber = entity.JobNumber;

                    // =========================
                    // 🔥 SAVE CHILD (INVOICE PAYMENTS)
                    // =========================
                    await SaveInvoicePayments(model, jobId, jobNumber, false);

                    // =========================
                    // 🔥 FINAL SAVE + COMMIT
                    // =========================
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new { errorCode = 200, message = "Saved successfully" };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    return new
                    {
                        errorCode = 500,
                        message = ex.InnerException?.Message ?? ex.Message
                    };
                }
            }
        }

        public async Task<dynamic> Save_Bk2(JobImportMasterVM model)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    JobImportMaster entity;

                    // =========================
                    // 🔹 INSERT
                    // =========================
                    if (model.Id == 0)
                    {
                        int newId = (await _context.JobImportMasters
                            .MaxAsync(x => (int?)x.Id) ?? 0) + 1;

                        entity = new JobImportMaster
                        {
                            Id = newId,
                            JobNumber = $"JBN-{newId:D6}",

                            CostCenterId = model.CostCenterId,
                            JobDate = model.JobDate,

                            ShipmentModeId = model.ShipmentModeId,
                            JobTypeId = model.JobTypeId,
                            OtherJobType = model.OtherJobType,

                            CustomerId = model.CustomerId,
                            Shipper = model.Shipper,
                            Consignee = model.Consignee,
                            Carrier = model.Carrier,

                            BookingDate = model.BookingDate,
                            PolId = model.PolId,
                            PodId = model.PodId,

                            Etd = model.Etd,
                            Eta = model.Eta,

                            BayanNo = model.BayanNo,
                            BlNo = model.BlNo,

                            ContainerQuantity = model.ContainerQuantity,
                            ContainerNo = model.ContainerNo,
                            ContainerTypeId = model.ContainerTypeId,

                            BayanPrintDate = model.BayanPrintDate,
                            ShipmentClearedDate = model.ShipmentClearedDate,
                            DeliveryDate = model.DeliveryDate,
                            ArrivalDate = model.ArrivalDate,

                            DeliveryCityId = model.DeliveryCityId,
                            BlTypeId = model.BlTypeId,

                            ShipmentStatusId = model.ShipmentStatusId,

                            OurInvoiceNo = model.OurInvoiceNo,
                            CreditNoteNo = model.CreditNoteNo,
                            AddInvoiceNo = model.AddInvoiceNo,

                            Remarks = model.Remarks,

                            CreatedBy = _session.LoginId,
                            CreatedOn = DateTime.Now
                        };

                        _context.JobImportMasters.Add(entity);
                        await _context.SaveChangesAsync(); // 🔥 IMPORTANT (ID generate)
                    }

                    // =========================
                    // 🔹 UPDATE
                    // =========================
                    else
                    {
                        entity = await _context.JobImportMasters.FirstOrDefaultAsync(x => x.Id == model.Id);

                        if (entity == null)
                            return new { errorCode = 404, message = "Record not found" };

                        entity.CostCenterId = model.CostCenterId;
                        entity.JobDate = model.JobDate;

                        entity.ShipmentModeId = model.ShipmentModeId;
                        entity.JobTypeId = model.JobTypeId;
                        entity.OtherJobType = model.OtherJobType;

                        entity.CustomerId = model.CustomerId;
                        entity.Shipper = model.Shipper;
                        entity.Consignee = model.Consignee;
                        entity.Carrier = model.Carrier;

                        entity.BookingDate = model.BookingDate;
                        entity.PolId = model.PolId;
                        entity.PodId = model.PodId;

                        entity.Etd = model.Etd;
                        entity.Eta = model.Eta;

                        entity.BayanNo = model.BayanNo;
                        entity.BlNo = model.BlNo;

                        entity.ContainerQuantity = model.ContainerQuantity;
                        entity.ContainerNo = model.ContainerNo;
                        entity.ContainerTypeId = model.ContainerTypeId;

                        entity.BayanPrintDate = model.BayanPrintDate;
                        entity.ShipmentClearedDate = model.ShipmentClearedDate;
                        entity.DeliveryDate = model.DeliveryDate;
                        entity.ArrivalDate = model.ArrivalDate;

                        entity.DeliveryCityId = model.DeliveryCityId;
                        entity.BlTypeId = model.BlTypeId;

                        entity.ShipmentStatusId = model.ShipmentStatusId;

                        entity.OurInvoiceNo = model.OurInvoiceNo;
                        entity.CreditNoteNo = model.CreditNoteNo;
                        entity.AddInvoiceNo = model.AddInvoiceNo;

                        entity.Remarks = model.Remarks;

                        entity.ModifiedBy = _session.LoginId;
                        entity.ModifiedOn = DateTime.Now;

                    }


                    await SaveInvoicePayments(model, entity.Id, "", true);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new { errorCode = 200, message = "Saved successfully" };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    return new
                    {
                        errorCode = 500,
                        message = ex.InnerException?.Message ?? ex.Message
                    };
                }
            }
        }

        public async Task<dynamic> Save_Bk(JobImportMasterVM model)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    JobImportMaster entity;

                    // =========================
                    // 🔹 INSERT
                    // =========================
                    if (model.Id == 0)
                    {
                        int newId = (await _context.JobImportMasters
                            .MaxAsync(x => (int?)x.Id) ?? 0) + 1;

                        entity = new JobImportMaster
                        {
                            Id = newId,
                            JobNumber = $"JBN-{newId:D6}",

                            CostCenterId = model.CostCenterId,
                            JobDate = model.JobDate,

                            ShipmentModeId = model.ShipmentModeId,
                            JobTypeId = model.JobTypeId,
                            OtherJobType = model.OtherJobType,

                            CustomerId = model.CustomerId,
                            Shipper = model.Shipper,
                            Consignee = model.Consignee,
                            Carrier = model.Carrier,

                            BookingDate = model.BookingDate,
                            PolId = model.PolId,
                            PodId = model.PodId,

                            Etd = model.Etd,
                            Eta = model.Eta,

                            BayanNo = model.BayanNo,
                            BlNo = model.BlNo,

                            ContainerQuantity = model.ContainerQuantity,
                            ContainerNo = model.ContainerNo,
                            ContainerTypeId = model.ContainerTypeId,

                            BayanPrintDate = model.BayanPrintDate,
                            ShipmentClearedDate = model.ShipmentClearedDate,
                            DeliveryDate = model.DeliveryDate,
                            ArrivalDate = model.ArrivalDate,

                            DeliveryCityId = model.DeliveryCityId,
                            BlTypeId = model.BlTypeId,

                            ShipmentStatusId = model.ShipmentStatusId,

                            OurInvoiceNo = model.OurInvoiceNo,
                            CreditNoteNo = model.CreditNoteNo,
                            AddInvoiceNo = model.AddInvoiceNo,

                            Remarks = model.Remarks,

                            CreatedBy = _session.LoginId,
                            CreatedOn = DateTime.Now
                        };

                        _context.JobImportMasters.Add(entity);
                        await _context.SaveChangesAsync(); // 🔥 IMPORTANT (ID generate)
                    }

                    // =========================
                    // 🔹 UPDATE
                    // =========================
                    else
                    {
                        entity = await _context.JobImportMasters
                            .FirstOrDefaultAsync(x => x.Id == model.Id);

                        if (entity == null)
                            return new { errorCode = 404, message = "Record not found" };

                        entity.CostCenterId = model.CostCenterId;
                        entity.JobDate = model.JobDate;

                        entity.ShipmentModeId = model.ShipmentModeId;
                        entity.JobTypeId = model.JobTypeId;
                        entity.OtherJobType = model.OtherJobType;

                        entity.CustomerId = model.CustomerId;
                        entity.Shipper = model.Shipper;
                        entity.Consignee = model.Consignee;
                        entity.Carrier = model.Carrier;

                        entity.BookingDate = model.BookingDate;
                        entity.PolId = model.PolId;
                        entity.PodId = model.PodId;

                        entity.Etd = model.Etd;
                        entity.Eta = model.Eta;

                        entity.BayanNo = model.BayanNo;
                        entity.BlNo = model.BlNo;

                        entity.ContainerQuantity = model.ContainerQuantity;
                        entity.ContainerNo = model.ContainerNo;
                        entity.ContainerTypeId = model.ContainerTypeId;

                        entity.BayanPrintDate = model.BayanPrintDate;
                        entity.ShipmentClearedDate = model.ShipmentClearedDate;
                        entity.DeliveryDate = model.DeliveryDate;
                        entity.ArrivalDate = model.ArrivalDate;

                        entity.DeliveryCityId = model.DeliveryCityId;
                        entity.BlTypeId = model.BlTypeId;

                        entity.ShipmentStatusId = model.ShipmentStatusId;

                        entity.OurInvoiceNo = model.OurInvoiceNo;
                        entity.CreditNoteNo = model.CreditNoteNo;
                        entity.AddInvoiceNo = model.AddInvoiceNo;

                        entity.Remarks = model.Remarks;

                        entity.ModifiedBy = _session.LoginId;
                        entity.ModifiedOn = DateTime.Now;

                        // 🔥 IMPORTANT: DELETE OLD PAYMENTS FIRST
                        var oldPayments = _context.JobImportPayments
                            .Where(x => x.JobImportMasterId == entity.Id);

                        _context.JobImportPayments.RemoveRange(oldPayments);

                        await _context.SaveChangesAsync(); // 🔥 CRITICAL (prevents duplicates)
                    }

                    // =========================
                    // 🔥 INSERT PAYMENTS (COMMON FOR BOTH)
                    // =========================

                    if (model.payment_type != null && model.payment_type.Count > 0)
                    {
                        // =========================
                        // 1. Build structured rows
                        // =========================
                        var rows = new List<JobImportPayment>();

                        int count = new[]
                        {
                            model.payment_type?.Count ?? 0,
                            model.payment_headers?.Count ?? 0,
                            model.invoice_no?.Count ?? 0,
                            model.invoice_date?.Count ?? 0,
                            model.amount?.Count ?? 0,
                            model.payment_reference?.Count ?? 0,
                            model.paid_by?.Count ?? 0
                        }.Min();

                        for (int i = 0; i < count; i++)
                        {
                            rows.Add(new JobImportPayment
                            {
                                PaymentType = model.payment_type?[i],
                                PaymentHeaderId = model.payment_headers?[i],
                                InvoiceNo = model.invoice_no?[i],
                                InvoiceDate = model.invoice_date?[i],
                                Amount = model.amount?[i],
                                PaymentReference = model.payment_reference?[i],
                                PaidBy = model.paid_by?[i],
                                CreatedBy = _session.LoginId,
                                CreatedOn = DateTime.Now
                            });
                        }

                        // =========================
                        // 2. REMOVE DUPLICATES
                        // =========================
                        var distinctRows = rows
                            .GroupBy(x => new
                            {
                                x.PaymentType,
                                x.PaymentHeaderId,
                                x.InvoiceNo,
                                x.InvoiceDate,
                                x.Amount,
                                x.PaymentReference,
                                x.PaidBy
                            })
                            .Select(g => g.First())
                            .ToList();

                        // =========================
                        // 3. SAVE CLEAN DATA
                        // =========================
                        foreach (var r in distinctRows)
                        {
                            var payment = new JobImportPayment
                            {
                                JobImportMasterId = entity.Id,

                                PaymentType = r.PaymentType,
                                PaymentHeaderId = r.PaymentHeaderId,

                                InvoiceNo = r.InvoiceNo,
                                InvoiceDate = r.InvoiceDate,

                                Amount = r.Amount,
                                PaymentReference = r.PaymentReference,
                                PaidBy = r.PaidBy
                            };

                            _context.JobImportPayments.Add(payment);
                        }
                    }

                    //if (model.payment_type != null && model.payment_type.Count > 0)
                    //{
                    //    for (int i = 0; i < model.payment_type.Count; i++)
                    //    {
                    //        var payment = new JobImportPayment
                    //        {
                    //            JobImportMasterId = entity.Id,

                    //            PaymentType = model.payment_type[i],
                    //            PaymentHeaderId = model.payment_headers?[i],

                    //            InvoiceNo = model.invoice_no?[i],
                    //            InvoiceDate = model.invoice_date?[i],

                    //            Amount = model.amount?[i],
                    //            PaymentReference = model.payment_reference?[i],
                    //            PaidBy = model.paid_by?[i]
                    //        };

                    //        _context.JobImportPayments.Add(payment);
                    //    }
                    //}

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new { errorCode = 200, message = "Saved successfully" };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    return new
                    {
                        errorCode = 500,
                        message = ex.InnerException?.Message ?? ex.Message
                    };
                }
            }
        }

        public async Task<dynamic> SaveInvoicePayments(JobImportMasterVM model, int jobImportMasterId, string jobNumber, bool hasLockEntry)
        {
            var existingPayments = await _context.JobImportPayments
                .Where(x => x.JobImportMasterId == jobImportMasterId)
                .ToListAsync();

            if (model?.payment_type == null || !model.payment_type.Any())
                return new { errorCode = 200, message = "No invoice rows available" };

            int count = new[]
            {
                model.payment_type?.Count ?? 0,
                model.payment_headers?.Count ?? 0,
                model.invoice_no?.Count ?? 0,
                model.invoice_date?.Count ?? 0,
                model.amount?.Count ?? 0,
                model.payment_reference?.Count ?? 0,
                model.paid_by?.Count ?? 0
            }.Min();

            int rowPointer = 0;
            bool hasValidRow = false; // 🔥 important

            for (int i = 0; i < count; i++)
            {
                var newPaymentType = model.payment_type?.ElementAtOrDefault(i);
                var newHeader = model.payment_headers?.ElementAtOrDefault(i);
                var newInvoiceNo = model.invoice_no?.ElementAtOrDefault(i);
                var newInvoiceDate = model.invoice_date?.ElementAtOrDefault(i);
                var newAmount = model.amount?.ElementAtOrDefault(i);
                var newRef = model.payment_reference?.ElementAtOrDefault(i);
                var newPaidBy = model.paid_by?.ElementAtOrDefault(i);

                // 🔥 Empty check
                bool isEmpty =
                    (newPaymentType == null || newPaymentType.ToString().Trim() == "") &&
                    (newHeader == null || newHeader.ToString().Trim() == "") &&
                    (string.IsNullOrWhiteSpace(newInvoiceNo)) &&
                    (newInvoiceDate == null) &&
                    (newAmount == null || newAmount == 0) &&
                    (string.IsNullOrWhiteSpace(newRef)) &&
                    (string.IsNullOrWhiteSpace(newPaidBy));

                if (isEmpty)
                    continue;

                hasValidRow = true; // ✅ at least one valid row found

                if (rowPointer < existingPayments.Count)
                {
                    var row = existingPayments[rowPointer];

                    bool isChanged =
                        row.PaymentType != newPaymentType ||
                        row.PaymentHeaderId != newHeader ||
                        row.InvoiceNo != newInvoiceNo ||
                        row.InvoiceDate != newInvoiceDate ||
                        row.Amount != newAmount ||
                        row.PaymentReference != newRef ||
                        row.PaidBy != newPaidBy;

                    if (isChanged)
                    {
                        row.PaymentType = newPaymentType;
                        row.PaymentHeaderId = newHeader;
                        row.InvoiceNo = newInvoiceNo;
                        row.InvoiceDate = newInvoiceDate;
                        row.Amount = newAmount;
                        row.PaymentReference = newRef;
                        row.PaidBy = newPaidBy;

                        row.ModifiedBy = _session.LoginId;
                        row.ModifiedOn = DateTime.Now;
                    }
                }
                else
                {
                    _context.JobImportPayments.Add(new JobImportPayment
                    {
                        JobImportMasterId = jobImportMasterId,

                        PaymentType = newPaymentType,
                        PaymentHeaderId = newHeader,
                        InvoiceNo = newInvoiceNo,
                        InvoiceDate = newInvoiceDate,
                        Amount = newAmount,
                        PaymentReference = newRef,
                        PaidBy = newPaidBy,

                        CreatedBy = _session.LoginId,
                        CreatedOn = DateTime.Now
                    });
                }

                rowPointer++;
            }

            await _context.SaveChangesAsync();

            // 🔥 ONLY call lock if at least one valid row exists
            if (hasValidRow && hasLockEntry && _session.UserType == 4)
            {
                await CreateInvoiceLockEntry(jobImportMasterId, jobNumber);
            }

            return new { errorCode = 200, message = "Saved successfully" };
        }

        public async Task<dynamic> GetJobs(IList<QueryFilters> filters)
        {
            int draw = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "draw")?.filterValue ?? "0");
            int start = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "start")?.filterValue ?? "0");
            int length = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "length")?.filterValue ?? "10");

            string userId = filters.FirstOrDefault(x => x.fieldName == "UserId")?.filterValue;
            string blNumber = filters.FirstOrDefault(x => x.fieldName == "BLNumber")?.filterValue;
            string fromDate = filters.FirstOrDefault(x => x.fieldName == "FromDate")?.filterValue;
            string toDate = filters.FirstOrDefault(x => x.fieldName == "ToDate")?.filterValue;
            string shipmentType = filters.FirstOrDefault(x => x.fieldName == "ShipmentType")?.filterValue;

            // 🔥 SORTING PARAMS (NEW ADD)
            int orderColumnIndex = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "orderColumn")?.filterValue ?? "0");
            string orderDir = filters.FirstOrDefault(x => x.fieldName == "orderDir")?.filterValue ?? "asc";

            var columns = new[]
            {
                "Id",
                "JobNo",
                "UserName",
                "CostCenter",
                "CustomerName",
                "Carrier",
                "JobDate",
                "ModeOfShipment",
                "BLNumber",
                "POL",
                "POD",
                "ContainerType",
                "ContainerNo",
                "BLStatus"
            };

            var query =
                from jm in _context.JobImportMasters

                    // CREATED BY
                join creator in _context.Users
                    on jm.CreatedBy equals creator.Id into creatorJoin
                from creator in creatorJoin.DefaultIfEmpty()

                    // LOGIN TYPE
                join lt in _context.LoginTypes
                    on creator.LoginType equals lt.Id into ltJoin
                from lt in ltJoin.DefaultIfEmpty()

                    // COST CENTER
                join cc in _context.CostCenters
                    on jm.CostCenterId equals cc.Id into ccJoin
                from cc in ccJoin.DefaultIfEmpty()

                    // CUSTOMER
                join c in _context.Customers
                    on jm.CustomerId equals c.Id into custJoin
                from c in custJoin.DefaultIfEmpty()

                    // POL
                join pol in _context.Pols
                    on jm.PolId equals pol.Id into polJoin
                from pol in polJoin.DefaultIfEmpty()

                    // POD
                join pod in _context.Pods
                    on jm.PodId equals pod.Id into podJoin
                from pod in podJoin.DefaultIfEmpty()

                    // CONTAINER TYPE
                join ct in _context.ContainerTypes
                    on jm.ContainerTypeId equals ct.Id into ctJoin
                from ct in ctJoin.DefaultIfEmpty()

                    // MODE
                join mos in _context.ShipmentModes
                    on jm.ShipmentModeId equals mos.Id into mosJoin
                from mos in mosJoin.DefaultIfEmpty()

                    // STATUS
                join ss in _context.ShipmentStatuses
                    on jm.ShipmentStatusId equals ss.Id into ssJoin
                from ss in ssJoin.DefaultIfEmpty()

                    // 🔥 IMPORTANT FIX (NO DUPLICATES)
                where _context.UserCostCenters
                    .Any(ucc => ucc.CostCenterId == jm.CostCenterId)

                select new
                {
                    jm.Id,
                    jm.ShipmentType,
                    JobNo = jm.JobNumber,
                    CreatedByUser = creator != null ? creator.UserName : "",
                    LoginTypeName = lt != null ? lt.Name : "",
                    ReferenceId = jm.ReferenceId,
                    CreatedBy = jm.CreatedBy,
                    CostCenter = cc.CostCenterName,

                    // optional: if you still want assigned user
                    AssignedUser = _context.Users
                        .Where(u => _context.UserCostCenters
                            .Any(x => x.UserId == u.Id && x.CostCenterId == jm.CostCenterId))
                        .Select(u => u.UserName)
                        .FirstOrDefault(),

                    CustomerName = c.CustomerName,
                    Carrier = jm.Carrier,
                    JobDate = jm.JobDate,
                    ModeOfShipment = mos.Name,
                    BLNumber = jm.BlNo,
                    POL = pol.Name,
                    POD = pod.Name,
                    ContainerType = ct.Name,
                    ContainerNo = jm.ContainerNo,
                    ShipmentStatus = ss.Name,
                    jm.CostCenterId
                };


            // Display only records created by the logged-in user
            if (_session.LoginType != 1)
            {
                query = query.Where(x => x.ReferenceId == _session.ReferenceId);
            }

            if (!string.IsNullOrEmpty(shipmentType))
            {
                int typeId = Convert.ToInt32(shipmentType);

                query = query.Where(x => x.ShipmentType == typeId);
            }

            // 🔍 FILTERS
            if (!string.IsNullOrEmpty(userId))
            {
                int uid = Convert.ToInt32(userId);

                query = query.Where(x => x.CreatedBy == uid);
            }

            if (!string.IsNullOrEmpty(fromDate))
            {
                DateOnly from = DateOnly.Parse(fromDate);
                query = query.Where(x => x.JobDate >= from);
            }

            if (!string.IsNullOrEmpty(toDate))
            {
                DateOnly to = DateOnly.Parse(toDate);
                query = query.Where(x => x.JobDate <= to);
            }

            if (!string.IsNullOrEmpty(blNumber))
                query = query.Where(x => x.BLNumber.Contains(blNumber));



            var allowedCostCenters = new List<int>();
            string costCenterIds = _session.CostCenterIds;

            if (!string.IsNullOrEmpty(costCenterIds))
            {
                allowedCostCenters = costCenterIds
                    .Split(',')
                    .Select(x => Convert.ToInt32(x))
                    .ToList();
            }

            if (_session.UserType != 1 && _session.UserType != 2)
            {
                query = query.Where(x =>
                    allowedCostCenters.Contains(x.CostCenterId));
            }

            //query = query.Where(x =>
            //        allowedCostCenters.Contains(x.CostCenterId));


            int totalRecords = await query.CountAsync();

            // SORTING LOGIC

            if (orderColumnIndex >= 0 && orderColumnIndex < columns.Length)
            {
                string sortColumn = columns[orderColumnIndex];

                if (orderDir == "asc")
                {
                    query = query.OrderBy(x => EF.Property<object>(x, sortColumn));
                }
                else
                {
                    query = query.OrderByDescending(x => EF.Property<object>(x, sortColumn));
                }
            }
            else
            {
                query = query.OrderByDescending(x => x.Id);
            }

            var sql = query.ToQueryString();

            var data = await query
                .Skip(start)
                .Take(length)
                .ToListAsync();

            return new
            {
                SqlQuery = sql,
                draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecords,
                data,
                errorCode = 200
            };
        }

        public async Task<dynamic> GetAllJobById(int id)
        {
            try
            {
                SqlParameter[] parameter =
                {
                    new SqlParameter { ParameterName = "@JobId", Value = id }
                };

                var result = await _db.Fetch(
                    "sp_GetAllJobImportById",
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
                    "Job Import",
                    "GetJobImportById",
                    ex.InnerException?.Message ?? ex.Message
                );

                return new
                {
                    errorCode = 999,
                    errorMessage = ex.Message
                };
            }
        }

        public async Task<dynamic> GetJobById(int id, int? paymentTypeId = null)
        {
            try
            {
                SqlParameter[] parameter =
                {
                    new SqlParameter { ParameterName = "@JobId", Value = id },
                    new SqlParameter { ParameterName = "@PaymentTypeId", Value = paymentTypeId.HasValue ? (object)paymentTypeId.Value : DBNull.Value }
                };

                var result = await _db.Fetch(
                    "sp_GetJobImportById",
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
                    "Job Import",
                    "GetJobById",
                    ex.InnerException?.Message ?? ex.Message
                );

                return new
                {
                    errorCode = 999,
                    errorMessage = ex.Message
                };
            }
        }

        public async Task CreateInvoiceLockEntry(int jobImportMasterId, string jobNumber)
        {
            int userId = _session.LoginId;

            // 🔥 Check existing unapproved request
            bool alreadyExists = await _context.JobInvoiceAccessRequests
                .AnyAsync(x =>
                    x.JobImportMasterId == jobImportMasterId &&
                    x.RequestedBy == userId &&
                    x.IsApproved == false && x.ReferenceId == _session.ReferenceId);

            // ❌ If already pending request exists → do nothing
            if (alreadyExists)
                return;

            var request = new JobInvoiceAccessRequest
            {
                ReferenceId = _session.ReferenceId,
                JobImportMasterId = jobImportMasterId,
                JobNumber = jobNumber,
                RequestedBy = userId,
                RequestedOn = DateTime.Now,
                IsApproved = false,
                Remarks = "Auto locked after first invoice submission"
            };

            _context.JobInvoiceAccessRequests.Add(request);
            await _context.SaveChangesAsync();
        }

        public async Task<dynamic> CheckInvoiceAccess(int jobId, int userId)
        {
            var accessRequest = await _context.JobInvoiceAccessRequests
                .Where(x => x.JobImportMasterId == jobId
                         && x.RequestedBy == userId)
                .OrderByDescending(x => x.RequestedOn)
                .FirstOrDefaultAsync();

            // ❌ If request exists but NOT approved → block
            if (accessRequest != null && accessRequest.IsApproved != true)
            {
                return new
                {
                    errorCode = 403,
                    message = "Access denied. Your request is not approved yet."
                };
            }

            // ✅ If approved OR no request → allow
            return new
            {
                errorCode = 200,
                message = "Access granted."
            };
        }

        public async Task<bool> CanAccessInvoice(int jobId, int userId)
        {
            var access = await _context.JobInvoiceAccessRequests
                .Where(x => x.JobImportMasterId == jobId
                         && x.RequestedBy == userId)
                .OrderByDescending(x => x.RequestedOn)
                .FirstOrDefaultAsync();

            // Allowed if:
            // 1. No request exists
            // 2. OR request is approved
            return access == null || access.IsApproved == true;
        }

        public async Task<bool> CanAccessJob(int jobId, int userId)
        {
            var userCostCenters = _context.UserCostCenters
                .Where(x => x.UserId == userId)
                .Select(x => x.CostCenterId);

            return await _context.JobImportMasters
                .AnyAsync(x =>
                    x.Id == jobId &&
                    userCostCenters.Contains(x.CostCenterId));
        }

        public async Task<dynamic> CheckInvoiceAccess_Bk1(int jobId, int userId)
        {
            // 🔹 Get first invoice entry (or latest)
            var invoice = await _context.JobImportPayments
                .Where(x => x.JobImportMasterId == jobId)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();

            // No invoice → allow
            if (invoice == null)
            {
                return new
                {
                    errorCode = 200,
                    message = "Access granted."
                };
            }

            // 🔥 If SAME user → apply lock
            if (invoice.CreatedBy == userId)
            {
                var request = await _context.JobInvoiceAccessRequests
                    .Where(x => x.JobImportMasterId == jobId
                             && x.RequestedBy == userId)
                    .OrderByDescending(x => x.RequestedOn)
                    .FirstOrDefaultAsync();

                if (request != null && request.IsApproved)
                {
                    return new
                    {
                        errorCode = 200,
                        message = "Access granted."
                    };
                }

                return new
                {
                    errorCode = 403,
                    message = "You already submitted this invoice. Request admin approval to edit."
                };
            }

            // 🔹 Different user → allow (optional rule)
            return new
            {
                errorCode = 200,
                message = "Access granted."
            };
        }

        public async Task<dynamic> RequestInvoiceAccess(int jobId)
        {
            var existing = await _context.JobInvoiceAccessRequests
                .FirstOrDefaultAsync(x =>
                    x.JobImportMasterId == jobId &&
                    x.RequestedBy == _session.LoginId &&
                    !x.IsApproved);

            if (existing != null)
            {
                return new
                {
                    errorCode = 409,
                    message = "Your request is already pending approval."
                };
            }

            var request = new JobInvoiceAccessRequest
            {
                ReferenceId = _session.ReferenceId,
                JobImportMasterId = jobId,
                RequestedBy = _session.LoginId,
                RequestedOn = DateTime.Now,
                IsApproved = false
            };

            _context.JobInvoiceAccessRequests.Add(request);
            await _context.SaveChangesAsync();

            return new
            {
                errorCode = 200,
                message = "Approval request sent successfully."
            };
        }

        public async Task<dynamic> GetPendingRequests()
        {
            var data = await (
                from req in _context.JobInvoiceAccessRequests
                join usr in _context.Users
                    on req.RequestedBy equals usr.Id
                where !req.IsApproved && req.ReferenceId == _session.ReferenceId
                orderby req.RequestedOn descending
                select new
                {
                    req.Id,
                    req.JobImportMasterId,
                    req.JobNumber,
                    req.RequestedBy,
                    UserName = usr.UserName, // 🔥 important
                    req.RequestedOn,
                    req.IsApproved
                }
            ).ToListAsync();

            return data;
        }

        public async Task<dynamic> ApproveInvoiceAccess(int requestId)
        {
            var request = await _context.JobInvoiceAccessRequests
                .FirstOrDefaultAsync(x => x.Id == requestId);

            if (request == null)
            {
                return new
                {
                    errorCode = 404,
                    message = "Request not found."
                };
            }
            

            request.IsApproved = true;
            request.ApprovedBy = _session.LoginId;
            request.ApprovedOn = DateTime.Now;

            await _context.SaveChangesAsync();

            return new
            {
                errorCode = 200,
                message = "Request approved successfully."
            };
        }

        #region Reports

        public async Task<dynamic> GetJobsReport_Bk(IList<QueryFilters> filters)
        {
            int draw = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "draw")?.filterValue ?? "0");
            int start = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "start")?.filterValue ?? "0");
            int length = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "length")?.filterValue ?? "10");

            string userId = filters.FirstOrDefault(x => x.fieldName == "UserId")?.filterValue;
            string blNumber = filters.FirstOrDefault(x => x.fieldName == "BLNumber")?.filterValue;
            string fromDate = filters.FirstOrDefault(x => x.fieldName == "FromDate")?.filterValue;
            string toDate = filters.FirstOrDefault(x => x.fieldName == "ToDate")?.filterValue;
            string shipmentType = filters.FirstOrDefault(x => x.fieldName == "ShipmentType")?.filterValue;

            int orderColumnIndex = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "orderColumn")?.filterValue ?? "0");
            string orderDir = filters.FirstOrDefault(x => x.fieldName == "orderDir")?.filterValue ?? "asc";

            var columns = new[]
            {
                "JobNo",
                "AssignedUser",
                "CostCenter",
                "CustomerName",
                "Carrier",
                "JobDate",
                "ModeOfShipment",
                "ShipmentType",
                "BLNumber",
                "POL",
                "POD",
                "ContainerType",
                "ContainerNo",
                "ETD",
                "SIReceivedDate",
                "ManifestReceive",
                "ShipmentStatus",
                "SailedDate",
                "TotalJobAmount",
                "Remarks"
            };

            var query =
                from jm in _context.JobImportMasters

                join creator in _context.Users
                    on jm.CreatedBy equals creator.Id into creatorJoin
                from creator in creatorJoin.DefaultIfEmpty()

                join cc in _context.CostCenters
                    on jm.CostCenterId equals cc.Id into ccJoin
                from cc in ccJoin.DefaultIfEmpty()

                join c in _context.Customers
                    on jm.CustomerId equals c.Id into custJoin
                from c in custJoin.DefaultIfEmpty()

                join pol in _context.Pols
                    on jm.PolId equals pol.Id into polJoin
                from pol in polJoin.DefaultIfEmpty()

                join pod in _context.Pods
                    on jm.PodId equals pod.Id into podJoin
                from pod in podJoin.DefaultIfEmpty()

                join ct in _context.ContainerTypes
                    on jm.ContainerTypeId equals ct.Id into ctJoin
                from ct in ctJoin.DefaultIfEmpty()

                join mos in _context.ShipmentModes
                    on jm.ShipmentModeId equals mos.Id into mosJoin
                from mos in mosJoin.DefaultIfEmpty()

                join ss in _context.ShipmentStatuses
                    on jm.ShipmentStatusId equals ss.Id into ssJoin
                from ss in ssJoin.DefaultIfEmpty()

                where _context.UserCostCenters.Any(ucc => ucc.CostCenterId == jm.CostCenterId)

                select new
                {
                    jm.Id,

                    AssignedUser = _context.Users
                        .Where(u => _context.UserCostCenters
                            .Any(x => x.UserId == u.Id && x.CostCenterId == jm.CostCenterId))
                        .Select(u => u.UserName)
                        .FirstOrDefault(),
                    JobNo = jm.JobNumber,
                    CostCenter = cc.CostCenterName,
                    CustomerName = c.CustomerName,
                    Carrier = jm.Carrier,
                    JobDate = jm.JobDate,
                    ModeOfShipment = mos.Name,
                    ShipmentType = jm.ShipmentType,
                    ShipmentTypeName = jm.ShipmentType == 1 ? "Import" : "Export",
                    BLNumber = jm.BlNo,
                    POL = pol.Name,
                    POD = pod.Name,
                    ContainerType = ct.Name,
                    ContainerNo = jm.ContainerNo,
                    ETD = jm.Etd,
                    SIReceivedDate = jm.SiReceivedDate,
                    ManifestReceive = jm.ManifestReceivedDate,
                    ShipmentStatus = ss.Name,
                    SailedDate = jm.SailDate,
                    TotalJobAmount = _context.JobImportPayments.Where(d => d.JobImportMasterId == jm.Id).Sum(d => (decimal?)d.Amount) ?? 0,
                    Remarks = jm.Remarks,
                    CostCenterId = jm.CostCenterId
                };

            // FILTERS (same logic)
            if (!string.IsNullOrEmpty(userId))
            {
                int uid = Convert.ToInt32(userId);
                query = query.Where(x =>
                    _context.UserCostCenters.Any(ucc => ucc.UserId == uid && ucc.CostCenterId == x.CostCenterId));
            }

            if (!string.IsNullOrEmpty(fromDate))
            {
                DateOnly from = DateOnly.Parse(fromDate);
                query = query.Where(x => x.JobDate >= from);
            }

            if (!string.IsNullOrEmpty(toDate))
            {
                DateOnly to = DateOnly.Parse(toDate);
                query = query.Where(x => x.JobDate <= to);
            }

            if (!string.IsNullOrEmpty(blNumber))
                query = query.Where(x => x.BLNumber.Contains(blNumber));

            if (!string.IsNullOrEmpty(shipmentType))
            {
                int typeId = Convert.ToInt32(shipmentType);
                query = query.Where(x => x.ShipmentType == typeId);
            }

            int totalRecords = await query.CountAsync();

            if (orderColumnIndex >= 0 && orderColumnIndex < columns.Length)
            {
                string sortColumn = columns[orderColumnIndex];

                query = orderDir == "asc"
                    ? query.OrderBy(x => EF.Property<object>(x, sortColumn))
                    : query.OrderByDescending(x => EF.Property<object>(x, sortColumn));
            }
            else
            {
                query = query.OrderByDescending(x => x.Id);
            }

            var data = await query.Skip(start).Take(length).ToListAsync();

            return new
            {
                draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecords,
                data,
                errorCode = 200
            };
        }

        public async Task<dynamic> GetJobsReport(IList<QueryFilters> filters)
        {
            int draw = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "draw")?.filterValue ?? "0");
            int start = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "start")?.filterValue ?? "0");
            int length = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "length")?.filterValue ?? "10");

            string userId = filters.FirstOrDefault(x => x.fieldName == "UserId")?.filterValue;
            string blNumber = filters.FirstOrDefault(x => x.fieldName == "BLNumber")?.filterValue;
            string fromDate = filters.FirstOrDefault(x => x.fieldName == "FromDate")?.filterValue;
            string toDate = filters.FirstOrDefault(x => x.fieldName == "ToDate")?.filterValue;
            string shipmentType = filters.FirstOrDefault(x => x.fieldName == "ShipmentType")?.filterValue;

            int orderColumnIndex = Convert.ToInt32(filters.FirstOrDefault(x => x.fieldName == "orderColumn")?.filterValue ?? "0");
            string orderDir = filters.FirstOrDefault(x => x.fieldName == "orderDir")?.filterValue ?? "asc";

            var columns = new[]
            {
                "JobNo",
                "AssignedUser",
                "CostCenter",
                "CustomerName",
                "Carrier",
                "JobDate",
                "ModeOfShipment",
                "ShipmentType",
                "BLNumber",
                "POL",
                "POD",
                "ContainerType",
                "ContainerNo",
                "ETD",
                "SIReceivedDate",
                "ManifestReceive",
                "ShipmentStatus",
                "SailedDate",
                "TotalJobAmount",
                "Remarks"
            };

            var query =
                from jm in _context.JobImportMasters

                join creator in _context.Users
                    on jm.CreatedBy equals creator.Id into creatorJoin
                from creator in creatorJoin.DefaultIfEmpty()

                join c in _context.Customers
                    on jm.CustomerId equals c.Id into custJoin
                from c in custJoin.DefaultIfEmpty()

                join pol in _context.Pols
                    on jm.PolId equals pol.Id into polJoin
                from pol in polJoin.DefaultIfEmpty()

                join pod in _context.Pods
                    on jm.PodId equals pod.Id into podJoin
                from pod in podJoin.DefaultIfEmpty()

                join ct in _context.ContainerTypes
                    on jm.ContainerTypeId equals ct.Id into ctJoin
                from ct in ctJoin.DefaultIfEmpty()

                join mos in _context.ShipmentModes
                    on jm.ShipmentModeId equals mos.Id into mosJoin
                from mos in mosJoin.DefaultIfEmpty()

                join ss in _context.ShipmentStatuses
                    on jm.ShipmentStatusId equals ss.Id into ssJoin
                from ss in ssJoin.DefaultIfEmpty()

                    // MULTI COST CENTER (bridge table)
                select new
                {
                    jm.Id,

                    JobNo = jm.JobNumber,
                    jm.CreatedBy,
                    AssignedUser = _context.Users
                        .Where(u => u.Id == jm.CreatedBy)
                        .Select(u => u.UserName)
                        .FirstOrDefault(),

                    // ✅ MULTI COST CENTER FIX
                    CostCenter = string.Join(", ",
                        (from ucc in _context.UserCostCenters
                         join cc in _context.CostCenters
                            on ucc.CostCenterId equals cc.Id
                         where ucc.CostCenterId == jm.CostCenterId
                         select cc.CostCenterName)
                        .Distinct()
                        .ToList()
                    ),

                    CustomerName = c.CustomerName,
                    Carrier = jm.Carrier,
                    JobDate = jm.JobDate,
                    ModeOfShipment = mos.Name,

                    ShipmentType = jm.ShipmentType,
                    ShipmentTypeName = jm.ShipmentType == 1 ? "Import" : "Export",

                    BLNumber = jm.BlNo,
                    POL = pol.Name,
                    POD = pod.Name,
                    ContainerType = ct.Name,
                    ContainerNo = jm.ContainerNo,

                    ETD = jm.Etd,
                    SIReceivedDate = jm.SiReceivedDate,
                    ManifestReceive = jm.ManifestReceivedDate,

                    ShipmentStatus = ss.Name,
                    SailedDate = jm.SailDate,
                    ReferenceId = jm.ReferenceId,
                    // TOTAL FROM INVOICE DETAILS
                    TotalJobAmount = _context.JobImportPayments
                        .Where(d => d.JobImportMasterId == jm.Id)
                        .Sum(d => (decimal?)d.Amount) ?? 0,

                    Remarks = jm.Remarks
                };

            // FILTERS
            query = query.Where(x => x.ReferenceId == _session.ReferenceId);

            if (!string.IsNullOrEmpty(userId))
            {
                int uid = Convert.ToInt32(userId);

                query = query.Where(x => x.CreatedBy == uid);
            }

            if (!string.IsNullOrEmpty(fromDate))
            {
                DateOnly from = DateOnly.Parse(fromDate);
                query = query.Where(x => x.JobDate >= from);
            }

            if (!string.IsNullOrEmpty(toDate))
            {
                DateOnly to = DateOnly.Parse(toDate);
                query = query.Where(x => x.JobDate <= to);
            }

            if (!string.IsNullOrEmpty(blNumber))
                query = query.Where(x => x.BLNumber.Contains(blNumber));

            if (!string.IsNullOrEmpty(shipmentType))
            {
                int typeId = Convert.ToInt32(shipmentType);
                query = query.Where(x => x.ShipmentType == typeId);
            }

            int totalRecords = await query.CountAsync();

            // SORTING
            if (orderColumnIndex >= 0 && orderColumnIndex < columns.Length)
            {
                string sortColumn = columns[orderColumnIndex];

                query = orderDir == "asc"
                    ? query.OrderBy(x => EF.Property<object>(x, sortColumn))
                    : query.OrderByDescending(x => EF.Property<object>(x, sortColumn));
            }
            else
            {
                query = query.OrderByDescending(x => x.Id);
            }

            var data = await query
                .Skip(start)
                .Take(length)
                .ToListAsync();

            return new
            {
                draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecords,
                data,
                errorCode = 200
            };
        }

        #endregion

    }
}
