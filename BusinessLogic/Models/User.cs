using System;
using System.Collections.Generic;

namespace BusinessLogic.Models;

public partial class User
{
    public int Id { get; set; }

    public int? CompanyId { get; set; }

    public string? UserId { get; set; }

    public string? Password { get; set; }

    public string? UserName { get; set; }

    public int? LoginType { get; set; }

    public int? ReferenceId { get; set; }

    public int? UserType { get; set; }

    public int? CostCenter { get; set; }

    public string? Email { get; set; }

    public string? PhoneNo { get; set; }

    public string? Cnic { get; set; }

    public string? Photo { get; set; }

    public bool? IsActive { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? CreatedOn { get; set; }

    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public DateTime? LastLogin { get; set; }

    public string? CompanyName { get; set; }

    public string? CompanyNameAr { get; set; }

    public string? EstablishmentName { get; set; }

    public string? EstablishmentNameAr { get; set; }

    public string? Country { get; set; }

    public string? CountryAr { get; set; }

    public string? City { get; set; }

    public string? CityAr { get; set; }

    public string? Vatnumber { get; set; }
}
