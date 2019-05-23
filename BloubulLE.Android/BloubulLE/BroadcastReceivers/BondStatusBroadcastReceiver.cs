using System;
using Android.Bluetooth;
using Android.Content;
using DH.BloubulLE.EventArgs;

namespace DH.BloubulLE.BroadcastReceivers
{
    //[BroadcastReceiver]
    public class BondStatusBroadcastReceiver : BroadcastReceiver
    {
        public event EventHandler<DeviceBondStateChangedEventArgs> BondStateChanged;

        public override void OnReceive(Context context, Intent intent)
        {
            Bond bondState = (Bond) intent.GetIntExtra(BluetoothDevice.ExtraBondState, (Int32) Bond.None);
            //ToDo
            Device device = new Device(null, (BluetoothDevice) intent.GetParcelableExtra(BluetoothDevice.ExtraDevice),
                null, 0);
            Console.WriteLine(bondState.ToString());

            if (this.BondStateChanged == null) return;

            switch (bondState)
            {
                case Bond.None:
                    this.BondStateChanged(this,
                        new DeviceBondStateChangedEventArgs {Device = device, State = DeviceBondState.NotBonded});
                    break;

                case Bond.Bonding:
                    this.BondStateChanged(this,
                        new DeviceBondStateChangedEventArgs {Device = device, State = DeviceBondState.Bonding});
                    break;

                case Bond.Bonded:
                    this.BondStateChanged(this,
                        new DeviceBondStateChangedEventArgs {Device = device, State = DeviceBondState.Bonded});
                    break;
            }
        }
    }
}