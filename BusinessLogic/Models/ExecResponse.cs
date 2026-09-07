using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Models
{
    public class ResponseMessage
    {
        public bool IsSuccess { get; set; }
        public string? ErrorCode { get; set; }
        public string? Message { get; set; }
        public dynamic? Data { get; set; }
    }
}
