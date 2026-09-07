using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Helper
{
    public static class StatusHelper
    {
        public static string GetInvoiceStatus(int status)
        {
            return status switch
            {
                0 => "UnPaid",
                1 => "Paid",
                2 => "Partially Paid",
                _ => "Unknown"
            };
        }
    }
}
