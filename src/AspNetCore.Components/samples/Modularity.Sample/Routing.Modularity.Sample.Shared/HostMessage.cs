using System;
using System.Collections.Generic;
using System.Text;

namespace Routing.Modularity.Sample
{
    public sealed class HostMessage
    {
        public HostMessage(string str)
        {
            Str = str;
        }

        public string Str { get; }
    }
}
