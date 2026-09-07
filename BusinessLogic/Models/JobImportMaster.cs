using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class JobImportMaster
{
    public int Id { get; set; }

    public int? ShipmentType { get; set; }

    public int CostCenterId { get; set; }

    public string? JobNumber { get; set; }

    public DateOnly JobDate { get; set; }

    public int ShipmentModeId { get; set; }

    public int JobTypeId { get; set; }

    public string? OtherJobType { get; set; }

    public int CustomerId { get; set; }

    public string Shipper { get; set; } = null!;

    public string Consignee { get; set; } = null!;

    public string? Carrier { get; set; }

    public DateOnly? BookingDate { get; set; }

    public int PolId { get; set; }

    public int PodId { get; set; }

    public DateOnly? Etd { get; set; }

    public DateOnly? Eta { get; set; }

    public string? BayanNo { get; set; }

    public string? BlNo { get; set; }

    public string ContainerQuantity { get; set; } = null!;

    public string ContainerNo { get; set; } = null!;

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

    public int? ReferenceId { get; set; }

    public int CreatedBy { get; set; }

    public DateTime? CreatedOn { get; set; }

    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }
}
