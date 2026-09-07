using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class JobImportMasterVM
{
    public int Id { get; set; }
    public int ShipmentType { get; set; }
    public string JobNumber { get; set; }
    public int CostCenterId { get; set; }
    public DateOnly JobDate { get; set; }

    public int ShipmentModeId { get; set; }
    public int JobTypeId { get; set; }
    public string? OtherJobType { get; set; }

    public int CustomerId { get; set; }

    public string Shipper { get; set; }
    public string Consignee { get; set; }
    public string? Carrier { get; set; }

    public DateOnly? BookingDate { get; set; }

    public int PolId { get; set; }
    public int PodId { get; set; }

    public DateOnly? Etd { get; set; }
    public DateOnly? Eta { get; set; }

    public string? BayanNo { get; set; }
    public string? BlNo { get; set; }

    public string ContainerQuantity { get; set; }
    public string ContainerNo { get; set; }
    public int ContainerTypeId { get; set; }

    public DateOnly? BayanPrintDate { get; set; }
    public DateOnly? ShipmentClearedDate { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public DateOnly? ArrivalDate { get; set; }

    public int DeliveryCityId { get; set; }
    public int? BlTypeId { get; set; }

    public string? BookingNo { get; set; }

    public DateOnly? SiReceivedDate { get; set; }

    public DateOnly? ManifestReceivedDate { get; set; }

    public DateOnly? SailDate { get; set; }

    public int? BlStatusId { get; set; }

    public int ShipmentStatusId { get; set; }

    public string? OurInvoiceNo { get; set; }
    public string? CreditNoteNo { get; set; }
    public string? AddInvoiceNo { get; set; }

    public string? Remarks { get; set; }

    //public List<int?> payment_type { get; set; } = new();
    //public List<int?> payment_headers { get; set; } = new();
    //public List<string> invoice_no { get; set; } = new();
    //public List<DateTime?> invoice_date { get; set; } = new();
    //public List<decimal?> amount { get; set; } = new();
    //public List<string> payment_reference { get; set; } = new();
    //public List<string> paid_by { get; set; } = new();

    public List<int?> payment_type { get; set; }
    public List<int?> payment_headers { get; set; }
    public List<string> invoice_no { get; set; }
    public List<DateTime?> invoice_date { get; set; }
    public List<decimal?> amount { get; set; } = new();
    public List<string> payment_reference { get; set; }
    public List<string> paid_by { get; set; }
}
