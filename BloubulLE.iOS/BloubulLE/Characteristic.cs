using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoreBluetooth;
using DH.BloubulLE.Contracts;
using DH.BloubulLE.EventArgs;
using DH.BloubulLE.Exceptions;
using DH.BloubulLE.Extensions;
using DH.BloubulLE.Utils;
using Foundation;

namespace DH.BloubulLE
{
    public class Characteristic : CharacteristicBase
    {
        private readonly CBCentralManager _centralManager;
        private readonly CBCharacteristic _nativeCharacteristic;
        private readonly CBPeripheral _parentDevice;

        public Characteristic(CBCharacteristic nativeCharacteristic, CBPeripheral parentDevice, IService service,
            CBCentralManager centralManager)
            : base(service)
        {
            this._nativeCharacteristic = nativeCharacteristic;
            this._parentDevice = parentDevice;
            this._centralManager = centralManager;
        }

        public override Guid Id => this._nativeCharacteristic.UUID.GuidFromUuid();
        public override String Uuid => this._nativeCharacteristic.UUID.ToString();

        public override Byte[] Value
        {
            get
            {
                NSData value = this._nativeCharacteristic.Value;
                if (value == null || value.Length == 0) return new Byte[0];

                return value.ToArray();
            }
        }

        public override CharacteristicPropertyType Properties =>
            (CharacteristicPropertyType) (Int32) this._nativeCharacteristic.Properties;

        public override event EventHandler<CharacteristicUpdatedEventArgs> ValueUpdated;

        protected override Task<IList<IDescriptor>> GetDescriptorsNativeAsync()
        {
            Exception exception =
                new Exception(
                    $"Device '{this.Service.Device.Id}' disconnected while fetching descriptors for characteristic with {this.Id}.");

            return TaskBuilder
                .FromEvent<IList<IDescriptor>, EventHandler<CBCharacteristicEventArgs>,
                    EventHandler<CBPeripheralErrorEventArgs>>(
                    () =>
                    {
                        if (this._parentDevice.State != CBPeripheralState.Connected)
                            throw exception;

                        this._parentDevice.DiscoverDescriptors(this._nativeCharacteristic);
                    },
                    (complete, reject) => (sender, args) =>
                    {
                        if (args.Characteristic.UUID != this._nativeCharacteristic.UUID)
                            return;

                        if (args.Error != null)
                            reject(new Exception($"Discover descriptors error: {args.Error.Description}"));
                        else
                            complete(args.Characteristic.Descriptors.Select(descriptor =>
                                    new Descriptor(descriptor, this._parentDevice, this, this._centralManager))
                                .Cast<IDescriptor>().ToList());
                    },
                    handler => this._parentDevice.DiscoveredDescriptor += handler,
                    handler => this._parentDevice.DiscoveredDescriptor -= handler,
                    reject => (sender, args) =>
                    {
                        if (args.Peripheral.Identifier == this._parentDevice.Identifier)
                            reject(exception);
                    },
                    handler => this._centralManager.DisconnectedPeripheral += handler,
                    handler => this._centralManager.DisconnectedPeripheral -= handler);
        }

        protected override Task<Byte[]> ReadNativeAsync()
        {
            Exception exception =
                new Exception(
                    $"Device '{this.Service.Device.Id}' disconnected while reading characteristic with {this.Id}.");

            return TaskBuilder
                .FromEvent<Byte[], EventHandler<CBCharacteristicEventArgs>, EventHandler<CBPeripheralErrorEventArgs>>(
                    () =>
                    {
                        if (this._parentDevice.State != CBPeripheralState.Connected)
                            throw exception;

                        this._parentDevice.ReadValue(this._nativeCharacteristic);
                    },
                    (complete, reject) => (sender, args) =>
                    {
                        if (args.Characteristic.UUID != this._nativeCharacteristic.UUID)
                            return;

                        if (args.Error != null)
                        {
                            reject(new CharacteristicReadException($"Read async error: {args.Error.Description}"));
                        }
                        else
                        {
                            Trace.Message($"Read characterteristic value: {this.Value?.ToHexString()}");
                            complete(this.Value);
                        }
                    },
                    handler => this._parentDevice.UpdatedCharacterteristicValue += handler,
                    handler => this._parentDevice.UpdatedCharacterteristicValue -= handler,
                    reject => (sender, args) =>
                    {
                        if (args.Peripheral.Identifier == this._parentDevice.Identifier)
                            reject(exception);
                    },
                    handler => this._centralManager.DisconnectedPeripheral += handler,
                    handler => this._centralManager.DisconnectedPeripheral -= handler);
        }

        protected override Task<Boolean> WriteNativeAsync(Byte[] data, CharacteristicWriteType writeType)
        {
            Exception exception =
                new Exception(
                    $"Device {this.Service.Device.Id} disconnected while writing characteristic with {this.Id}.");

            Task<Boolean> task;
            if (writeType.ToNative() == CBCharacteristicWriteType.WithResponse)
                task = TaskBuilder
                    .FromEvent<Boolean, EventHandler<CBCharacteristicEventArgs>,
                        EventHandler<CBPeripheralErrorEventArgs>>(
                        () =>
                        {
                            if (this._parentDevice.State != CBPeripheralState.Connected)
                                throw exception;
                        },
                        (complete, reject) => (sender, args) =>
                        {
                            if (args.Characteristic.UUID != this._nativeCharacteristic.UUID)
                                return;

                            complete(args.Error == null);
                        },
                        handler => this._parentDevice.WroteCharacteristicValue += handler,
                        handler => this._parentDevice.WroteCharacteristicValue -= handler,
                        reject => (sender, args) =>
                        {
                            if (args.Peripheral.Identifier == this._parentDevice.Identifier)
                                reject(exception);
                        },
                        handler => this._centralManager.DisconnectedPeripheral += handler,
                        handler => this._centralManager.DisconnectedPeripheral -= handler);
            else
                task = Task.FromResult(true);

            NSData nsdata = NSData.FromArray(data);
            this._parentDevice.WriteValue(nsdata, this._nativeCharacteristic, writeType.ToNative());

            return task;
        }

        protected override Task StartUpdatesNativeAsync()
        {
            Exception exception =
                new Exception(
                    $"Device {this.Service.Device.Id} disconnected while starting updates for characteristic with {this.Id}.");

            this._parentDevice.UpdatedCharacterteristicValue -= this.UpdatedNotify;
            this._parentDevice.UpdatedCharacterteristicValue += this.UpdatedNotify;

            //https://developer.apple.com/reference/corebluetooth/cbperipheral/1518949-setnotifyvalue
            return TaskBuilder
                .FromEvent<Boolean, EventHandler<CBCharacteristicEventArgs>, EventHandler<CBPeripheralErrorEventArgs>>(
                    () =>
                    {
                        if (this._parentDevice.State != CBPeripheralState.Connected)
                            throw exception;

                        this._parentDevice.SetNotifyValue(true, this._nativeCharacteristic);
                    },
                    (complete, reject) => (sender, args) =>
                    {
                        if (args.Characteristic.UUID != this._nativeCharacteristic.UUID)
                            return;

                        if (args.Error != null)
                        {
                            reject(new Exception($"Start Notifications: Error {args.Error.Description}"));
                        }
                        else
                        {
                            Trace.Message($"StartUpdates IsNotifying: {args.Characteristic.IsNotifying}");
                            complete(args.Characteristic.IsNotifying);
                        }
                    },
                    handler => this._parentDevice.UpdatedNotificationState += handler,
                    handler => this._parentDevice.UpdatedNotificationState -= handler,
                    reject => (sender, args) =>
                    {
                        if (args.Peripheral.Identifier == this._parentDevice.Identifier)
                            reject(new Exception(
                                $"Device {this.Service.Device.Id} disconnected while starting updates for characteristic with {this.Id}."));
                    },
                    handler => this._centralManager.DisconnectedPeripheral += handler,
                    handler => this._centralManager.DisconnectedPeripheral -= handler);
        }

        protected override Task StopUpdatesNativeAsync()
        {
            Exception exception =
                new Exception(
                    $"Device {this.Service.Device.Id} disconnected while stopping updates for characteristic with {this.Id}.");

            this._parentDevice.UpdatedCharacterteristicValue -= this.UpdatedNotify;

            return TaskBuilder
                .FromEvent<Boolean, EventHandler<CBCharacteristicEventArgs>, EventHandler<CBPeripheralErrorEventArgs>>(
                    () =>
                    {
                        if (this._parentDevice.State != CBPeripheralState.Connected)
                            throw exception;

                        this._parentDevice.SetNotifyValue(false, this._nativeCharacteristic);
                    },
                    (complete, reject) => (sender, args) =>
                    {
                        if (args.Characteristic.UUID != this._nativeCharacteristic.UUID)
                            return;

                        if (args.Error != null)
                        {
                            reject(new Exception($"Stop Notifications: Error {args.Error.Description}"));
                        }
                        else
                        {
                            Trace.Message($"StopUpdates IsNotifying: {args.Characteristic.IsNotifying}");
                            complete(args.Characteristic.IsNotifying);
                        }
                    },
                    handler => this._parentDevice.UpdatedNotificationState += handler,
                    handler => this._parentDevice.UpdatedNotificationState -= handler,
                    reject => (sender, args) =>
                    {
                        if (args.Peripheral.Identifier == this._parentDevice.Identifier)
                            reject(exception);
                    },
                    handler => this._centralManager.DisconnectedPeripheral += handler,
                    handler => this._centralManager.DisconnectedPeripheral -= handler);
        }

        private void UpdatedNotify(Object sender, CBCharacteristicEventArgs e)
        {
            if (e.Characteristic.UUID == this._nativeCharacteristic.UUID)
                this.ValueUpdated?.Invoke(this, new CharacteristicUpdatedEventArgs(this));
        }
    }
}