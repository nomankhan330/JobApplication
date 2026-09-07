using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Models
{
    public partial class UserC
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

        public string? CNIC { get; set; }

        public string? Photo { get; set; }

        public bool? IsActive { get; set; }

        public int? CreatedBy { get; set; }

        public DateTime? CreatedOn { get; set; }

        public int? ModifiedBy { get; set; }

        public DateTime? ModifiedOn { get; set; }

        public DateTime? LastLogin { get; set; }


    }
}
