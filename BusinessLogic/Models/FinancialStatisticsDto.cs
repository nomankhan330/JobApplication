using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Models
{
    public class FinancialStatisticsDto
    {
        public decimal SalesRevenue { get; set; }
        public decimal SalesRevenuePrevious { get; set; }

        public decimal SpOutstanding { get; set; }

        public decimal PurchaseCurrent { get; set; }
        public decimal PurchasePrevious { get; set; }

        public decimal NetCashInflow { get; set; }
        public decimal NetCashInflowPrevious { get; set; }
    }
}
