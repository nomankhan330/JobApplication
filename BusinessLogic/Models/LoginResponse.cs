using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Models
{
    public class LoginResponse
    {
        public int errorCode { get; set; }
        public string errorMessage { get; set; }
        public int accountId { get; set; }
        public string accountName { get; set; }
        public int loginId { get; set; }
        public string userName { get; set; }
        public DateTime expiryDate { get; set; }
        public string token { get; set; }
    }
}
