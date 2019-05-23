using System.Diagnostics;

namespace DH.BloubulLE
{
    internal static class DefaultTrace
    {
        static DefaultTrace()
        {
            //uses WriteLine for trace
            Trace.TraceImplementation = (s, o) => { Debug.WriteLine(s, o); };
        }
    }
}