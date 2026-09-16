using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Models
{
    public class RecentInvoiceDto
    {
        public string InvoiceNo { get; set; }
        public string JobNo { get; set; }
        public string PartyName { get; set; }
        public string Type { get; set; }          // "Sales" or "Purchase"
        public string BLNumber { get; set; }
        public decimal GrandTotal { get; set; }
        public int Status { get; set; }         // "Paid", "Unpaid", "Partially Paid"
    }
}
