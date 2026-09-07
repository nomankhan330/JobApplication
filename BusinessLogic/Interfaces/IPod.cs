using BusinessLogic.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Interfaces
{
    public interface IPod : IDisposable
    {
        public Task<(int Id, string Name)> SavePodAsync(string podName);
    }
}
