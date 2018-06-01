using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace BasicMessaging
{
    public sealed class PrintCommand
    {
        public PrintCommand(ImmutableArray<string> component)
        {
            Component = component;
        }

        public ImmutableArray<string> Component { get; }
    }
}
