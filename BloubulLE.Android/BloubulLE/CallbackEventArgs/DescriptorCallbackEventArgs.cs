using System;
using Android.Bluetooth;

namespace DH.BloubulLE.CallbackEventArgs
{
    public class DescriptorCallbackEventArgs
    {
        public DescriptorCallbackEventArgs(BluetoothGattDescriptor descriptor, Exception exception = null)
        {
            this.Descriptor = descriptor;
            this.Exception = exception;
        }

        public BluetoothGattDescriptor Descriptor { get; }
        public Exception Exception { get; }
    }
}