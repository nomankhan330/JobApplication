using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Models
{
    public class ErrorResponse
    {
        public bool Error { get; set; }
        public List<ErrorList> ErrorList { get; set; }

        public ErrorResponse()
        {
            ErrorList = new List<ErrorList>();
        }
    }

    public class ErrorList
    {
        public int ErrorCode { get; set; }
        public string Message { get; set; }
        public string ErrorType { get; set; }
        public Level WarningLevel { get; set; }
        public string Description { get; set; }
    }

    public enum Level
    {
        Error,
        Warning,
        Information
    }
}
