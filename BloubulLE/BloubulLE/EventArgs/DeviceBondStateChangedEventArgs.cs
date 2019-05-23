using DH.BloubulLE.Contracts;

namespace DH.BloubulLE.EventArgs
{
    public class DeviceBondStateChangedEventArgs : System.EventArgs
    {
        public IDevice Device { get; set; }
        public DeviceBondState State { get; set; }
    }
}