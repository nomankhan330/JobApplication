using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Interfaces
{
    public interface ISBService
    {
        public Task<string> PostWithoutTokenAsync(string service, object? body);
        public Task<string> PostAsync(string service, object? body);
    }
}
