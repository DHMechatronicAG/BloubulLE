using System;

namespace DH.BloubulLE
{
    internal static class DefaultTrace
    {
        static DefaultTrace()
        {
            Trace.TraceImplementation = Console.WriteLine;
        }
    }
}