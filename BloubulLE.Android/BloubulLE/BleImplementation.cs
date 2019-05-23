using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using DH.BloubulLE.BroadcastReceivers;
using DH.BloubulLE.Contracts;
using DH.BloubulLE.Extensions;

namespace DH.BloubulLE
{
    public class BleImplementation : BleImplementationBase
    {
        private BluetoothManager _bluetoothManager;

        public BleImplementation()
        {
            this.Initialize();
        }

        protected override void InitializeNative()
        {
            Context ctx = Application.Context;
            if (!ctx.PackageManager.HasSystemFeature(PackageManager.FeatureBluetoothLe))
                return;

            BluetoothStatusBroadcastReceiver statusChangeReceiver =
                new BluetoothStatusBroadcastReceiver(this.UpdateState);
            ctx.RegisterReceiver(statusChangeReceiver, new IntentFilter(BluetoothAdapter.ActionStateChanged));

            this._bluetoothManager = (BluetoothManager) ctx.GetSystemService(Context.BluetoothService);
        }

        protected override BluetoothState GetInitialStateNative()
        {
            if (this._bluetoothManager == null)
                return BluetoothState.Unavailable;

            return this._bluetoothManager.Adapter.State.ToBluetoothState();
        }

        protected override IAdapter CreateNativeAdapter()
        {
            return new Adapter(this._bluetoothManager);
        }

        private void UpdateState(BluetoothState state)
        {
            this.State = state;
        }
    }
}