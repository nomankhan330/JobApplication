using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Models
{
    public class QueryFilters
    {
        public string fieldName { get; set; }
        public string operator_ { get; set; }
        public string filterValue { get; set; }
        public string condition { get; set; }
    }
}
