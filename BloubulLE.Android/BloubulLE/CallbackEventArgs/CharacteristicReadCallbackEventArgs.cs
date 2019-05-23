using Android.Bluetooth;

namespace DH.BloubulLE.CallbackEventArgs
{
    public class CharacteristicReadCallbackEventArgs
    {
        public CharacteristicReadCallbackEventArgs(BluetoothGattCharacteristic characteristic)
        {
            this.Characteristic = characteristic;
        }

        public BluetoothGattCharacteristic Characteristic { get; }
    }
}