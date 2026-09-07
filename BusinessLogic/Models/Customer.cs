using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class Customer
{
    public int Id { get; set; }

    public string? CustomerCode { get; set; }

    public int? TypeId { get; set; }

    public string Language { get; set; } = null!;

    public int CompanyId { get; set; }

    public string CustomerName { get; set; } = null!;

    public string? CustomerNameAr { get; set; }

    public string? MailingName { get; set; }

    public string? MailingNameAr { get; set; }

    public string Email { get; set; } = null!;

    public string? ContactNo { get; set; }

    public string? Address { get; set; }

    public string? AddressAr { get; set; }

    public string VatId { get; set; } = null!;

    public bool? VatDeduction { get; set; }

    public decimal VatRate { get; set; }

    public int? RegionId { get; set; }

    public int? CountryId { get; set; }

    public string? Pobox { get; set; }

    public bool? LocalLanguageMailing { get; set; }

    public bool? IsActive { get; set; }

    public string? BankName { get; set; }

    public string? AccountNumber { get; set; }

    public string? Iban { get; set; }

    public string? SwiftCode { get; set; }

    public int? ReferenceId { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? CreatedOn { get; set; }

    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }
}
