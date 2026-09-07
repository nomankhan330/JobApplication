using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Interfaces
{
    public interface ILogs : IDisposable
    {
        void Write(string message);
        void Write(string pageName, string functionName, string message);
        void Write(string pageName, string functionName, Exception e);
        void Write(Models.Logs logs);
    }
}
