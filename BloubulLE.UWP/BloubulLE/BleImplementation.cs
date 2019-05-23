using DH.BloubulLE.Contracts;
using Microsoft.Toolkit.Uwp.Connectivity;

namespace DH.BloubulLE
{
    public class BleImplementation : BleImplementationBase
    {
        private BluetoothLEHelper _bluetoothHelper;

        public BleImplementation()
        {
            this.Initialize();
        }

        protected override IAdapter CreateNativeAdapter()
        {
            return new Adapter(this._bluetoothHelper);
        }

        protected override BluetoothState GetInitialStateNative()
        {
            //The only way to get the state of bluetooth through windows is by
            //getting the radios for a device. This operation is asynchronous
            //and thus cannot be called in this method. Thus, we are just
            //returning "On" as long as the BluetoothLEHelper is initialized
            if (this._bluetoothHelper == null) return BluetoothState.Unavailable;
            return BluetoothState.On;
        }

        protected override void InitializeNative()
        {
            //create local helper using the app context
            BluetoothLEHelper localHelper = BluetoothLEHelper.Context;
            this._bluetoothHelper = localHelper;
        }
    }
}