using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BasicMessaging
{
    public interface ILogToConsole
    {
        Task LogAsync(string text);
    }
}
