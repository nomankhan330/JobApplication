using BusinessLogic.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Interfaces
{
    public interface IDeliveryCity : IDisposable
    {
        public Task<(int Id, string Name)> SaveDeliveryCityAsync(string deliveryCityName);
    }
}
