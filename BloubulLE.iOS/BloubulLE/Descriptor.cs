using System;
using System.Text;
using System.Threading.Tasks;
using CoreBluetooth;
using DH.BloubulLE.Contracts;
using DH.BloubulLE.Extensions;
using DH.BloubulLE.Utils;
using Foundation;

namespace DH.BloubulLE
{
    public class Descriptor : DescriptorBase
    {
        private readonly CBCentralManager _centralManager;
        private readonly CBDescriptor _nativeDescriptor;

        private readonly CBPeripheral _parentDevice;

        public Descriptor(CBDescriptor nativeDescriptor, CBPeripheral parentDevice, ICharacteristic characteristic,
            CBCentralManager centralManager)
            : base(characteristic)
        {
            this._parentDevice = parentDevice;
            this._nativeDescriptor = nativeDescriptor;
            this._centralManager = centralManager;
        }

        public override Guid Id => this._nativeDescriptor.UUID.GuidFromUuid();

        public override Byte[] Value
        {
            get
            {
                if (this._nativeDescriptor.Value is NSData) return ((NSData) this._nativeDescriptor.Value).ToArray();

                if (this._nativeDescriptor.Value is NSNumber)
                    return BitConverter.GetBytes(((NSNumber) this._nativeDescriptor.Value).UInt64Value);

                if (this._nativeDescriptor.Value is NSString)
                    return Encoding.UTF8.GetBytes(((NSString) this._nativeDescriptor.Value).ToString());

                //TODO https://developer.apple.com/reference/corebluetooth/cbuuid/1667288-characteristic_descriptors
                Trace.Message(
                    $"Descriptor: can't convert {this._nativeDescriptor.Value?.GetType().Name} with value {this._nativeDescriptor.Value} to byte[]");
                return null;
            }
        }

        protected override Task<Byte[]> ReadNativeAsync()
        {
            Exception exception =
                new Exception(
                    $"Device '{this.Characteristic.Service.Device.Id}' disconnected while reading descriptor with {this.Id}.");

            return TaskBuilder
                .FromEvent<Byte[], EventHandler<CBDescriptorEventArgs>, EventHandler<CBPeripheralErrorEventArgs>>(
                    () =>
                    {
                        if (this._parentDevice.State != CBPeripheralState.Connected)
                            throw exception;

                        this._parentDevice.ReadValue(this._nativeDescriptor);
                    },
                    (complete, reject) => (sender, args) =>
                    {
                        if (args.Descriptor.UUID != this._nativeDescriptor.UUID)
                            return;

                        if (args.Error != null)
                            reject(new Exception($"Read descriptor async error: {args.Error.Description}"));
                        else
                            complete(this.Value);
                    },
                    handler => this._parentDevice.UpdatedValue += handler,
                    handler => this._parentDevice.UpdatedValue -= handler,
                    reject => (sender, args) =>
                    {
                        if (args.Peripheral.Identifier == this._parentDevice.Identifier)
                            reject(exception);
                    },
                    handler => this._centralManager.DisconnectedPeripheral += handler,
                    handler => this._centralManager.DisconnectedPeripheral -= handler);
        }

        protected override Task WriteNativeAsync(Byte[] data)
        {
            Exception exception =
                new Exception(
                    $"Device '{this.Characteristic.Service.Device.Id}' disconnected while writing descriptor with {this.Id}.");

            return TaskBuilder
                .FromEvent<Boolean, EventHandler<CBDescriptorEventArgs>, EventHandler<CBPeripheralErrorEventArgs>>(
                    () =>
                    {
                        if (this._parentDevice.State != CBPeripheralState.Connected)
                            throw exception;

                        this._parentDevice.WriteValue(NSData.FromArray(data), this._nativeDescriptor);
                    },
                    (complete, reject) => (sender, args) =>
                    {
                        if (args.Descriptor.UUID != this._nativeDescriptor.UUID)
                            return;

                        if (args.Error != null)
                            reject(new Exception(args.Error.Description));
                        else
                            complete(true);
                    },
                    handler => this._parentDevice.WroteDescriptorValue += handler,
                    handler => this._parentDevice.WroteDescriptorValue -= handler,
                    reject => (sender, args) =>
                    {
                        if (args.Peripheral.Identifier == this._parentDevice.Identifier)
                            reject(exception);
                    },
                    handler => this._centralManager.DisconnectedPeripheral += handler,
                    handler => this._centralManager.DisconnectedPeripheral -= handler);
        }
    }
}