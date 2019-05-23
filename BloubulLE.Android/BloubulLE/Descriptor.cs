using System;
using System.Threading.Tasks;
using Android.Bluetooth;
using DH.BloubulLE.CallbackEventArgs;
using DH.BloubulLE.Contracts;
using DH.BloubulLE.Utils;

namespace DH.BloubulLE
{
    public class Descriptor : DescriptorBase
    {
        private readonly BluetoothGatt _gatt;
        private readonly IGattCallback _gattCallback;
        private readonly BluetoothGattDescriptor _nativeDescriptor;

        public Descriptor(BluetoothGattDescriptor nativeDescriptor, BluetoothGatt gatt, IGattCallback gattCallback,
            ICharacteristic characteristic) : base(characteristic)
        {
            this._gattCallback = gattCallback;
            this._gatt = gatt;
            this._nativeDescriptor = nativeDescriptor;
        }

        public override Guid Id => Guid.ParseExact(this._nativeDescriptor.Uuid.ToString(), "d");

        public override Byte[] Value => this._nativeDescriptor.GetValue();

        protected override Task WriteNativeAsync(Byte[] data)
        {
            return TaskBuilder.FromEvent<Boolean, EventHandler<DescriptorCallbackEventArgs>, EventHandler>(
                () => this.InternalWrite(data),
                (complete, reject) => (sender, args) =>
                {
                    if (args.Descriptor.Uuid != this._nativeDescriptor.Uuid)
                        return;

                    if (args.Exception != null)
                        reject(args.Exception);
                    else
                        complete(true);
                },
                handler => this._gattCallback.DescriptorValueWritten += handler,
                handler => this._gattCallback.DescriptorValueWritten -= handler,
                reject => (sender, args) =>
                {
                    reject(new Exception(
                        $"Device '{this.Characteristic.Service.Device.Id}' disconnected while writing descriptor with {this.Id}."));
                },
                handler => this._gattCallback.ConnectionInterrupted += handler,
                handler => this._gattCallback.ConnectionInterrupted -= handler);
        }

        private void InternalWrite(Byte[] data)
        {
            if (!this._nativeDescriptor.SetValue(data))
                throw new Exception("GATT: SET descriptor value failed");

            if (!this._gatt.WriteDescriptor(this._nativeDescriptor))
                throw new Exception("GATT: WRITE descriptor value failed");
        }

        protected override async Task<Byte[]> ReadNativeAsync()
        {
            return await TaskBuilder.FromEvent<Byte[], EventHandler<DescriptorCallbackEventArgs>, EventHandler>(
                this.ReadInternal,
                (complete, reject) => (sender, args) =>
                {
                    if (args.Descriptor.Uuid == this._nativeDescriptor.Uuid) complete(args.Descriptor.GetValue());
                },
                handler => this._gattCallback.DescriptorValueRead += handler,
                handler => this._gattCallback.DescriptorValueRead -= handler,
                reject => (sender, args) =>
                {
                    reject(new Exception(
                        $"Device '{this.Characteristic.Service.Device.Id}' disconnected while reading descripor with {this.Id}."));
                },
                handler => this._gattCallback.ConnectionInterrupted += handler,
                handler => this._gattCallback.ConnectionInterrupted -= handler);
        }

        private void ReadInternal()
        {
            if (!this._gatt.ReadDescriptor(this._nativeDescriptor))
                throw new Exception("GATT: read characteristic FALSE");
        }
    }
}