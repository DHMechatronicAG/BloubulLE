using System;

namespace DH.BloubulLE.CallbackEventArgs
{
    public class RssiReadCallbackEventArgs : System.EventArgs
    {
        public RssiReadCallbackEventArgs(Exception error, Int32 rssi)
        {
            this.Error = error;
            this.Rssi = rssi;
        }

        public Exception Error { get; }
        public Int32 Rssi { get; }
    }
}