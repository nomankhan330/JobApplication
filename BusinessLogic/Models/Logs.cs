using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Models
{
    public class Logs
    {
        public Guid LogId { get; set; }
        public DateTime LogDate { get; set; }
        public string PageName { get; set; }
        public string FunctionName { get; set; }
        public string ErrorType { get; set; }
        public string Error { get; set; }
    }
}
