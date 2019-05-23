using System;

namespace DH.BloubulLE.CallbackEventArgs
{
    public class MtuRequestCallbackEventArgs : System.EventArgs
    {
        public MtuRequestCallbackEventArgs(Exception error, Int32 mtu)
        {
            this.Error = error;
            this.Mtu = mtu;
        }

        public Exception Error { get; }
        public Int32 Mtu { get; }
    }
}