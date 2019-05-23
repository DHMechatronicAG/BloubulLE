using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoreBluetooth;
using DH.BloubulLE.Contracts;
using DH.BloubulLE.Utils;
using Foundation;

namespace DH.BloubulLE
{
    public class Device : DeviceBase
    {
        private readonly CBCentralManager _centralManager;
        private readonly CBPeripheral _nativeDevice;

        public Device(Adapter adapter, CBPeripheral nativeDevice, CBCentralManager centralManager)
            : this(adapter, nativeDevice, centralManager, nativeDevice.Name, nativeDevice.RSSI?.Int32Value ?? 0,
                new List<AdvertisementRecord>())
        {
        }

        public Device(Adapter adapter, CBPeripheral nativeDevice, CBCentralManager centralManager, String name,
            Int32 rssi, List<AdvertisementRecord> advertisementRecords)
            : base(adapter)
        {
            this._nativeDevice = nativeDevice;
            this._centralManager = centralManager;

            this.Id = Guid.ParseExact(this._nativeDevice.Identifier.AsString(), "d");
            this.Name = name;

            this.Rssi = rssi;
            this.AdvertisementRecords = advertisementRecords;

            // TODO figure out if this is in any way required,  
            // https://github.com/xabre/xamarin-bluetooth-le/issues/81
            //_nativeDevice.UpdatedName += OnNameUpdated;
        }

        public override Object NativeDevice => this._nativeDevice;

        private void OnNameUpdated(Object sender, System.EventArgs e)
        {
            this.Name = ((CBPeripheral) sender).Name;
            Trace.Message("Device changed name: {0}", this.Name);
        }

        protected override Task<IEnumerable<IService>> GetServicesNativeAsync()
        {
            Exception exception = new Exception($"Device {this.Name} disconnected while fetching services.");

            return TaskBuilder
                .FromEvent<IEnumerable<IService>, EventHandler<NSErrorEventArgs>,
                    EventHandler<CBPeripheralErrorEventArgs>>(
                    () =>
                    {
                        if (this._nativeDevice.State != CBPeripheralState.Connected)
                            throw exception;

                        this._nativeDevice.DiscoverServices();
                    },
                    (complete, reject) => (sender, args) =>
                    {
                        // If args.Error was not null then the Service might be null
                        if (args.Error != null)
                        {
                            reject(new Exception(
                                $"Error while discovering services {args.Error.LocalizedDescription}"));
                        }
                        else if (this._nativeDevice.Services == null)
                        {
                            // No service discovered. 
                            reject(new Exception("Error while discovering services: returned list is null"));
                        }
                        else
                        {
                            List<IService> services = this._nativeDevice.Services
                                .Select(nativeService => new Service(nativeService, this, this._centralManager))
                                .Cast<IService>().ToList();
                            complete(services);
                        }
                    },
                    handler => this._nativeDevice.DiscoveredService += handler,
                    handler => this._nativeDevice.DiscoveredService -= handler,
                    reject => (sender, args) =>
                    {
                        if (args.Peripheral.Identifier == this._nativeDevice.Identifier)
                            reject(exception);
                    },
                    handler => this._centralManager.DisconnectedPeripheral += handler,
                    handler => this._centralManager.DisconnectedPeripheral -= handler);
        }

        public override async Task<Boolean> UpdateRssiAsync()
        {
            TaskCompletionSource<Boolean> tcs = new TaskCompletionSource<Boolean>();
            EventHandler<CBRssiEventArgs> handler = null;

            handler = (sender, args) =>
            {
                Trace.Message("Read RSSI async for {0} {1}: {2}", this.Id, this.Name, args.Rssi);

                this._nativeDevice.RssiRead -= handler;
                Boolean success = args.Error == null;

                if (success) this.Rssi = args.Rssi?.Int32Value ?? 0;

                tcs.TrySetResult(success);
            };

            this._nativeDevice.RssiRead += handler;
            this._nativeDevice.ReadRSSI();

            return await tcs.Task;
        }

        protected override DeviceState GetState()
        {
            switch (this._nativeDevice.State)
            {
                case CBPeripheralState.Connected:
                    return DeviceState.Connected;
                case CBPeripheralState.Connecting:
                    return DeviceState.Connecting;
                case CBPeripheralState.Disconnected:
                    return DeviceState.Disconnected;
                case CBPeripheralState.Disconnecting:
                    return DeviceState.Disconnected;
                default:
                    return DeviceState.Disconnected;
            }
        }

        public void Update(CBPeripheral nativeDevice)
        {
            this.Rssi = nativeDevice.RSSI?.Int32Value ?? 0;

            //It's maybe not the best idea to updated the name based on CBPeripherial name because this might be stale.
            //Name = nativeDevice.Name; 
        }

        protected override async Task<Int32> RequestMtuNativeAsync(Int32 requestValue)
        {
            Trace.Message("Request MTU is not supported on iOS.");
            return await Task.FromResult(
                (Int32) this._nativeDevice.GetMaximumWriteValueLength(CBCharacteristicWriteType.WithoutResponse));
        }

        protected override Boolean UpdateConnectionIntervalNative(ConnectionInterval interval)
        {
            Trace.Message("Cannot update connection inteval on iOS.");
            return false;
        }
    }
}