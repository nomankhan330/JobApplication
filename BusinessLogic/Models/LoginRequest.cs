using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Models
{
    public class LoginRequest
    {
        public string userid { get; set; }
        public string password { get; set; }
        public int companyid { get; set; }
    }
}
