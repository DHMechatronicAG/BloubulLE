using System;
using Android.Bluetooth;
using Android.Content;
using DH.BloubulLE.Contracts;
using DH.BloubulLE.Extensions;

namespace DH.BloubulLE.BroadcastReceivers
{
    public class BluetoothStatusBroadcastReceiver : BroadcastReceiver
    {
        private readonly Action<BluetoothState> _stateChangedHandler;

        public BluetoothStatusBroadcastReceiver(Action<BluetoothState> stateChangedHandler)
        {
            this._stateChangedHandler = stateChangedHandler;
        }

        public override void OnReceive(Context context, Intent intent)
        {
            String action = intent.Action;

            if (action != BluetoothAdapter.ActionStateChanged)
                return;

            Int32 state = intent.GetIntExtra(BluetoothAdapter.ExtraState, -1);

            if (state == -1)
            {
                this._stateChangedHandler?.Invoke(BluetoothState.Unknown);
                return;
            }

            State btState = (State) state;
            this._stateChangedHandler?.Invoke(btState.ToBluetoothState());
        }
    }
}