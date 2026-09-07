using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Models;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Blstatus> Blstatuses { get; set; }

    public virtual DbSet<Bltype> Bltypes { get; set; }

    public virtual DbSet<City> Cities { get; set; }

    public virtual DbSet<Company> Companies { get; set; }

    public virtual DbSet<ContainerType> ContainerTypes { get; set; }

    public virtual DbSet<CostCenter> CostCenters { get; set; }

    public virtual DbSet<Country> Countries { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<DeliveryCity> DeliveryCities { get; set; }

    public virtual DbSet<JobImportMaster> JobImportMasters { get; set; }

    public virtual DbSet<JobImportPayment> JobImportPayments { get; set; }

    public virtual DbSet<JobInvoiceAccessRequest> JobInvoiceAccessRequests { get; set; }

    public virtual DbSet<JobType> JobTypes { get; set; }

    public virtual DbSet<Log> Logs { get; set; }

    public virtual DbSet<LoginType> LoginTypes { get; set; }

    public virtual DbSet<PaymentHeader> PaymentHeaders { get; set; }

    public virtual DbSet<PaymentReceived> PaymentReceiveds { get; set; }

    public virtual DbSet<Pod> Pods { get; set; }

    public virtual DbSet<Pol> Pols { get; set; }

    public virtual DbSet<PurchaseCreditNote> PurchaseCreditNotes { get; set; }

    public virtual DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }

    public virtual DbSet<PurchaseInvoiceDetail> PurchaseInvoiceDetails { get; set; }

    public virtual DbSet<PurchaseVoucher> PurchaseVouchers { get; set; }

    public virtual DbSet<Region> Regions { get; set; }

    public virtual DbSet<SalesCreditNote> SalesCreditNotes { get; set; }

    public virtual DbSet<SalesInvoice> SalesInvoices { get; set; }

    public virtual DbSet<SalesInvoiceDetail> SalesInvoiceDetails { get; set; }

    public virtual DbSet<ShipmentMode> ShipmentModes { get; set; }

    public virtual DbSet<ShipmentStatus> ShipmentStatuses { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserCostCenter> UserCostCenters { get; set; }

    public virtual DbSet<UserType> UserTypes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Blstatus>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BLStatus__3214EC07C40B5212");

            entity.ToTable("BLStatus");

            entity.Property(e => e.Name).HasMaxLength(255);
        });

        modelBuilder.Entity<Bltype>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BLType__3214EC07F0531418");

            entity.ToTable("BLType");

            entity.Property(e => e.Name).HasMaxLength(255);
        });

        modelBuilder.Entity<City>(entity =>
        {
            entity.ToTable("City");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("Company");

            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.NameAr).HasMaxLength(255);
        });

        modelBuilder.Entity<ContainerType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Containe__3214EC0733B2A489");

            entity.ToTable("ContainerType");

            entity.Property(e => e.Name).HasMaxLength(255);
        });

        modelBuilder.Entity<CostCenter>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CostCent__3214EC073D044D00");

            entity.HasIndex(e => e.Id, "IX_CostCenters_Id");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Address)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Cnic)
                .HasMaxLength(25)
                .IsUnicode(false)
                .HasColumnName("CNIC");
            entity.Property(e => e.ContactNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ContactPerson)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.CostCenterCode)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.CostCenterName)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.MyPercentage).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Photo)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Country>(entity =>
        {
            entity.ToTable("Country");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC079A7068BC");

            entity.ToTable("Customer");

            entity.HasIndex(e => e.Id, "IX_Customer_Id");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.AccountNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.BankName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ContactNo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedOn)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.CustomerCode)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.CustomerName).HasMaxLength(200);
            entity.Property(e => e.CustomerNameAr).HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.Iban)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Language).HasMaxLength(10);
            entity.Property(e => e.MailingName).HasMaxLength(200);
            entity.Property(e => e.MailingNameAr).HasMaxLength(200);
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.Pobox)
                .HasMaxLength(50)
                .HasColumnName("POBox");
            entity.Property(e => e.SwiftCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.VatId).HasMaxLength(50);
            entity.Property(e => e.VatRate).HasColumnType("decimal(10, 2)");
        });

        modelBuilder.Entity<DeliveryCity>(entity =>
        {
            entity.ToTable("DeliveryCity");

            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<JobImportMaster>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__JobImpor__3214EC077214CF09");

            entity.ToTable("JobImportMaster");

            entity.HasIndex(e => e.CostCenterId, "IX_JobImportMaster_CostCenterId");

            entity.HasIndex(e => new { e.CostCenterId, e.JobDate }, "IX_JobImportMaster_CostCenter_Date");

            entity.HasIndex(e => e.JobDate, "IX_JobImportMaster_JobDate");

            entity.HasIndex(e => e.Id, "IX_JobImportMaster_Optimization");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.AddInvoiceNo).HasMaxLength(100);
            entity.Property(e => e.BayanNo).HasMaxLength(100);
            entity.Property(e => e.BlNo).HasMaxLength(100);
            entity.Property(e => e.BookingNo).HasMaxLength(100);
            entity.Property(e => e.Carrier).HasMaxLength(150);
            entity.Property(e => e.Consignee).HasMaxLength(200);
            entity.Property(e => e.ContainerQuantity).HasMaxLength(50);
            entity.Property(e => e.CreatedOn)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.CreditNoteNo).HasMaxLength(100);
            entity.Property(e => e.JobNumber).HasMaxLength(50);
            entity.Property(e => e.ModifiedOn)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.OtherJobType).HasMaxLength(100);
            entity.Property(e => e.OurInvoiceNo).HasMaxLength(100);
            entity.Property(e => e.Shipper).HasMaxLength(200);
        });

        modelBuilder.Entity<JobImportPayment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__JobImpor__3214EC07D664F0A2");

            entity.ToTable("JobImportPayment");

            entity.HasIndex(e => e.JobImportMasterId, "IX_JobImportPayment_JobImportMasterId");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.InvoiceDate).HasColumnType("datetime");
            entity.Property(e => e.InvoiceNo).HasMaxLength(100);
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.PaidBy).HasMaxLength(150);
            entity.Property(e => e.PaymentReference).HasMaxLength(255);
        });

        modelBuilder.Entity<JobInvoiceAccessRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__JobInvoi__3214EC07BE312A04");

            entity.Property(e => e.ApprovedOn).HasColumnType("datetime");
            entity.Property(e => e.JobNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Remarks).HasMaxLength(500);
            entity.Property(e => e.RequestedOn)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<JobType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__JobType__3214EC0793FF606D");

            entity.ToTable("JobType");

            entity.Property(e => e.Name).HasMaxLength(255);
        });

        modelBuilder.Entity<Log>(entity =>
        {
            entity.Property(e => e.LogId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.ErrorType).HasMaxLength(50);
            entity.Property(e => e.LogDate).HasColumnType("datetime");
            entity.Property(e => e.PageName)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<LoginType>(entity =>
        {
            entity.ToTable("LoginType");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<PaymentHeader>(entity =>
        {
            entity.ToTable("PaymentHeader");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.Headers)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.Vat)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("VAT");
        });

        modelBuilder.Entity<PaymentReceived>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__PaymentR__3214EC0726CF7433");

            entity.ToTable("PaymentReceived");

            entity.HasIndex(e => e.InvoiceId, "IX_PaymentReceived_InvoiceId");

            entity.HasIndex(e => new { e.InvoiceId, e.PaymentDate }, "IX_PaymentReceived_InvoiceId_PaymentDate");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.BlNo).HasMaxLength(100);
            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.PaymentDate).HasColumnType("datetime");
            entity.Property(e => e.PaymentMode).HasMaxLength(50);
            entity.Property(e => e.Rvnumber)
                .HasMaxLength(100)
                .HasColumnName("RVNumber");
        });

        modelBuilder.Entity<Pod>(entity =>
        {
            entity.ToTable("Pod");

            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Pol>(entity =>
        {
            entity.ToTable("Pol");

            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<PurchaseCreditNote>(entity =>
        {
            entity.HasKey(e => e.CreditNoteId).HasName("PK__Purchase__AF360DC614A7E013");

            entity.ToTable("PurchaseCreditNote");

            entity.Property(e => e.CreditNoteId).ValueGeneratedNever();
            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.CreditNoteDate).HasColumnType("datetime");
            entity.Property(e => e.CreditNoteNo).HasMaxLength(20);
            entity.Property(e => e.GrandTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InvoiceDate).HasColumnType("datetime");
            entity.Property(e => e.InvoiceNo).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.NetAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Remarks).HasMaxLength(500);
            entity.Property(e => e.VatAmount).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<PurchaseInvoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK__Purchase__D796AAB53A1F2C69");

            entity.ToTable("PurchaseInvoice");

            entity.HasIndex(e => e.InvoiceNo, "IX_PurchaseInvoice_InvoiceNo");

            entity.HasIndex(e => new { e.ServiceProviderId, e.InvoiceDate }, "IX_PurchaseInvoice_ServiceProviderId_InvoiceDate");

            entity.HasIndex(e => e.InvoiceNo, "UQ_PurchaseInvoice_InvoiceNo").IsUnique();

            entity.Property(e => e.Advance).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.BalanceAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CancelRemarks).HasMaxLength(500);
            entity.Property(e => e.CancelledOn).HasColumnType("datetime");
            entity.Property(e => e.CreatedOn)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Discount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.GrandTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InvoiceDate).HasColumnType("datetime");
            entity.Property(e => e.InvoiceNo).HasMaxLength(50);
            entity.Property(e => e.IsCancelled).HasDefaultValue(false);
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.NetAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaidAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ServiceProviderAddress)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.ServiceProviderCountry)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ServiceProviderName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ServiceProviderRegion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ServiceProviderVatId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.VatAmount).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<PurchaseInvoiceDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Purchase__3214EC0786DFE20C");

            entity.ToTable("PurchaseInvoiceDetail");

            entity.HasIndex(e => e.InvoiceId, "IX_PurchaseInvoiceDetail_InvoiceId");

            entity.HasIndex(e => e.PaymentHeaderId, "IX_PurchaseInvoiceDetail_PaymentHeaderId");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Rate).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.VatAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.VatPercent).HasColumnType("decimal(5, 2)");
        });

        modelBuilder.Entity<PurchaseVoucher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__PurchaseVoucher__3214EC0726CF7433");

            entity.ToTable("PurchaseVoucher");

            entity.HasIndex(e => new { e.InvoiceId, e.IsActive, e.PaymentDate }, "IX_PurchaseVoucher_InvoiceId_PaymentDate");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.BlNo).HasMaxLength(100);
            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.PaymentDate).HasColumnType("datetime");
            entity.Property(e => e.PaymentMode).HasMaxLength(50);
            entity.Property(e => e.Pvnumber)
                .HasMaxLength(100)
                .HasColumnName("PVNumber");
        });

        modelBuilder.Entity<Region>(entity =>
        {
            entity.ToTable("Region");

            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SalesCreditNote>(entity =>
        {
            entity.HasKey(e => e.CreditNoteId).HasName("PK__SaleCred__AF360DC6628BF95B");

            entity.ToTable("SalesCreditNote");

            entity.Property(e => e.CreditNoteId).ValueGeneratedNever();
            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.CreditNoteDate).HasColumnType("datetime");
            entity.Property(e => e.CreditNoteNo).HasMaxLength(20);
            entity.Property(e => e.GrandTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InvoiceDate).HasColumnType("datetime");
            entity.Property(e => e.InvoiceNo).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.NetAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Remarks).HasMaxLength(500);
            entity.Property(e => e.VatAmount).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<SalesInvoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK__SalesInv__D796AAB5F720A650");

            entity.ToTable("SalesInvoice");

            entity.HasIndex(e => new { e.CustomerId, e.InvoiceDate }, "IX_SalesInvoice_CustomerId_InvoiceDate");

            entity.HasIndex(e => e.CustomerId, "IX_SalesInvoice_CustomerId_InvoiceId");

            entity.HasIndex(e => e.JobId, "IX_SalesInvoice_JobId");

            entity.HasIndex(e => e.InvoiceNo, "UQ_SalesInvoice_InvoiceNo").IsUnique();

            entity.HasIndex(e => e.InvoiceNo, "UQ_SalesInvoices_InvoiceNo").IsUnique();

            entity.Property(e => e.Advance).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.BalanceAmount)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CancelRemarks).HasMaxLength(500);
            entity.Property(e => e.CancelledOn).HasColumnType("datetime");
            entity.Property(e => e.CreatedOn)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Discount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.GrandTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InvoiceDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.InvoiceNo).HasMaxLength(50);
            entity.Property(e => e.IsCancelled).HasDefaultValue(false);
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.NetAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaidAmount)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.VatAmount).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<SalesInvoiceDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__SalesInv__3214EC07CF06C757");

            entity.ToTable("SalesInvoiceDetail");

            entity.HasIndex(e => e.InvoiceId, "IX_SalesInvoiceDetail_InvoiceId");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Rate).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.VatAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.VatPercent).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<ShipmentMode>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Shipment__3214EC076DEAB46A");

            entity.ToTable("ShipmentMode");

            entity.HasIndex(e => e.Id, "IX_ShipmentMode_Id");

            entity.Property(e => e.Name).HasMaxLength(255);
        });

        modelBuilder.Entity<ShipmentStatus>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Shipment__3214EC07E66C53BD");

            entity.ToTable("ShipmentStatus");

            entity.Property(e => e.Name).HasMaxLength(255);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.CityAr).HasMaxLength(100);
            entity.Property(e => e.Cnic)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("CNIC");
            entity.Property(e => e.CompanyName).HasMaxLength(200);
            entity.Property(e => e.CompanyNameAr).HasMaxLength(200);
            entity.Property(e => e.Country).HasMaxLength(100);
            entity.Property(e => e.CountryAr).HasMaxLength(100);
            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.EstablishmentName).HasMaxLength(250);
            entity.Property(e => e.EstablishmentNameAr).HasMaxLength(250);
            entity.Property(e => e.LastLogin).HasColumnType("datetime");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.Password)
                .HasMaxLength(32)
                .IsUnicode(false);
            entity.Property(e => e.PhoneNo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Photo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UserId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UserName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Vatnumber).HasMaxLength(50);
        });

        modelBuilder.Entity<UserCostCenter>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.CostCenterId });

            entity.HasIndex(e => e.CostCenterId, "IX_UserCostCenters_CostCenterId");

            entity.HasIndex(e => e.UserId, "IX_UserCostCenters_UserId");
        });

        modelBuilder.Entity<UserType>(entity =>
        {
            entity.ToTable("UserType");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
        });
        modelBuilder.HasSequence("PurchaseInvoiceSequence").StartsAt(2L);
        modelBuilder.HasSequence("SalesInvoiceSequence");

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
