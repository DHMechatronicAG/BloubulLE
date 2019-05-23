using System;
using Android.Bluetooth;

namespace DH.BloubulLE.CallbackEventArgs
{
    public class CharacteristicWriteCallbackEventArgs
    {
        public CharacteristicWriteCallbackEventArgs(BluetoothGattCharacteristic characteristic,
            Exception exception = null)
        {
            this.Characteristic = characteristic;
            this.Exception = exception;
        }

        public BluetoothGattCharacteristic Characteristic { get; }
        public Exception Exception { get; }
    }
}