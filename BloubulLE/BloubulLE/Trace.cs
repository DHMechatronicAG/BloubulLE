using System;

namespace DH.BloubulLE
{
    public static class Trace
    {
        public static Action<String, Object[]> TraceImplementation { get; set; }

        public static void Message(String format, params Object[] args)
        {
            try
            {
                TraceImplementation?.Invoke(format, args);
            }
            catch
            {
                /* ignore */
            }
        }
    }
}