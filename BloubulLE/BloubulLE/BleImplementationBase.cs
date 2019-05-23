using System;
using System.Threading;
using DH.BloubulLE.Contracts;
using DH.BloubulLE.EventArgs;
using DH.BloubulLE.Utils;

namespace DH.BloubulLE
{
    public abstract class BleImplementationBase : IBluetoothLE
    {
        private readonly Lazy<IAdapter> _adapter;
        private BluetoothState _state;

        protected BleImplementationBase()
        {
            this._adapter = new Lazy<IAdapter>(this.CreateAdapter, LazyThreadSafetyMode.PublicationOnly);
        }

        public event EventHandler<BluetoothStateChangedArgs> StateChanged;

        public Boolean IsAvailable => this._state != BluetoothState.Unavailable;
        public Boolean IsOn => this._state == BluetoothState.On;
        public IAdapter Adapter => this._adapter.Value;

        public BluetoothState State
        {
            get => this._state;
            protected set
            {
                if (this._state == value)
                    return;

                BluetoothState oldState = this._state;
                this._state = value;
                this.StateChanged?.Invoke(this, new BluetoothStateChangedArgs(oldState, this._state));
            }
        }

        public void Initialize()
        {
            this.InitializeNative();
            this.State = this.GetInitialStateNative();
        }

        private IAdapter CreateAdapter()
        {
            if (!this.IsAvailable)
                return new FakeAdapter();

            return this.CreateNativeAdapter();
        }

        protected abstract void InitializeNative();
        protected abstract BluetoothState GetInitialStateNative();
        protected abstract IAdapter CreateNativeAdapter();
    }
}