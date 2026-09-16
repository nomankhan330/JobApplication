using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Models
{
    public class JobOperationsShareDto
    {
        public string Label { get; set; }       // Sea Import, Air Import, Local Log. etc.
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }
}
