using System;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Radios;
using DH.BloubulLE.Contracts;
using Microsoft.Toolkit.Uwp.Connectivity;

namespace DH.BloubulLE
{
    public class BleImplementation : BleImplementationBase
    {
        private BluetoothLEHelper _bluetoothHelper;
        private Radio DefaultRadio;

        public BleImplementation()
        {
            this.DefaultRadio = null;

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

        private BluetoothState RadioStateTo(RadioState State)
        {
            switch (State)
            {
                case RadioState.Off:
                    return BluetoothState.Off;
                case RadioState.On:
                    return BluetoothState.On;
                case RadioState.Disabled:
                    return BluetoothState.Unavailable;
                default:
                    return BluetoothState.Unknown;
            }
        }

        protected override async void InitializeNative()
        {
            //create local helper using the app context
            BluetoothLEHelper localHelper = BluetoothLEHelper.Context;
            this._bluetoothHelper = localHelper;

            // BloubulLE: We want to get the actually state and its changes.
            try
            {
                RadioAccessStatus tAccessStatus = await Radio.RequestAccessAsync();

                if (tAccessStatus == RadioAccessStatus.Allowed)
                {
                    BluetoothAdapter tAdapter = await BluetoothAdapter.GetDefaultAsync();

                    if(tAdapter != null)
                    {
                        this.DefaultRadio = await tAdapter.GetRadioAsync();

                        if (this.DefaultRadio != null)
                        {
                            this.DefaultRadio.StateChanged += this.OnRadioStateChanged;
                            this.OnRadioStateChanged(this.DefaultRadio, this);
                        }
                    }
                }
            }
            catch (Exception iEx)
            {
                //-
            }
        }

        private void OnRadioStateChanged(Radio Radio, object Sender)
        {
            this.State = this.RadioStateTo(Radio.State);
        }
    }
}