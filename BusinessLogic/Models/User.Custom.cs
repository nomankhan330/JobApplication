using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Models
{
    public partial class User
    {
        [NotMapped]
        public List<int> CostCenterIds { get; set; } = new();
    }
}
