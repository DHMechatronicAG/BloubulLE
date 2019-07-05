using CoreBluetooth;
using CoreFoundation;
using DH.BloubulLE.Contracts;
using DH.BloubulLE.Extensions;
using Foundation;

namespace DH.BloubulLE
{
    [Preserve(AllMembers = true)]
    public class BleImplementation : BleImplementationBase
    {
        private CBCentralManager _centralManager;

        public BleImplementation()
        {
            this.Initialize();
        }
        protected override void InitializeNative()
        {
            this._centralManager = new CBCentralManager(DispatchQueue.CurrentQueue);
            this._centralManager.UpdatedState += (s, e) => this.State = this.GetState();
        }

        protected override BluetoothState GetInitialStateNative()
        {
            return this.GetState();
        }

        protected override IAdapter CreateNativeAdapter()
        {
            return new Adapter(this._centralManager);
        }

        private BluetoothState GetState()
        {
            return this._centralManager.State.ToBluetoothState();
        }
    }
}