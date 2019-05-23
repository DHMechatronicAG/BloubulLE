using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using DH.BloubulLE.Contracts;
using Microsoft.Toolkit.Uwp.Connectivity;

namespace DH.BloubulLE
{
    internal class Device : DeviceBase
    {
        public delegate void ConnectionStatusChangedHandler(Device device, BluetoothConnectionStatus status);

        public ConnectionStatusChangedHandler ConnectionStatusChanged;

        public Device(Adapter adapter, BluetoothLEDevice nativeDevice, Int32 rssi, String address,
            List<AdvertisementRecord> advertisementRecords = null) : base(adapter)
        {
            this._nativeDevice = new ObservableBluetoothLEDevice(nativeDevice.DeviceInformation);
            this.Rssi = rssi;
            this.Id = this.ParseDeviceId(nativeDevice.BluetoothAddress.ToString("x"));
            this.Name = nativeDevice.Name;
            this.AdvertisementRecords = advertisementRecords;
            this._nativeDevice.PropertyChanged += this.NativeDevice_PropertyChanged;
        }

        public ObservableBluetoothLEDevice _nativeDevice { get; }
        public override Object NativeDevice => this._nativeDevice;

        private void NativeDevice_PropertyChanged(Object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "IsConnected")
                return;

            this.ConnectionStatusChanged?.Invoke(this, this._nativeDevice.BluetoothLEDevice.ConnectionStatus);
        }

        /// <summary>
        /// Method to parse the bluetooth address as a hex string to a UUID
        /// </summary>
        /// <param name="macWithoutColons">The bluetooth address as a hex string without colons</param>
        /// <returns>a GUID that is padded left with 0 and the last 6 bytes are the bluetooth address</returns>
        private Guid ParseDeviceId(String macWithoutColons)
        {
            macWithoutColons = macWithoutColons.PadLeft(12, '0'); //ensure valid length
            Byte[] deviceGuid = new Byte[16];
            Array.Clear(deviceGuid, 0, 16);
            Byte[] macBytes = Enumerable.Range(0, macWithoutColons.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(macWithoutColons.Substring(x, 2), 16))
                .ToArray();
            macBytes.CopyTo(deviceGuid, 10);
            return new Guid(deviceGuid);
        }

        public override Task<Boolean> UpdateRssiAsync()
        {
            //No current method to update the Rssi of a device
            //In future implementations, maybe listen for device's advertisements
            throw new NotImplementedException();
        }

        protected override async Task<IEnumerable<IService>> GetServicesNativeAsync()
        {
            IReadOnlyList<GattDeviceService> GattServiceList =
                (await this._nativeDevice.BluetoothLEDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached)).Services;
            List<IService> ServiceList = new List<IService>();
            foreach (GattDeviceService nativeService in GattServiceList)
            {
                Service service = new Service(nativeService, this);
                ServiceList.Add(service);
            }

            return ServiceList;
        }

        protected override DeviceState GetState()
        {
            //windows only supports retrieval of two states currently
            if (this._nativeDevice.IsConnected) return DeviceState.Connected;
            return DeviceState.Disconnected;
        }

        protected override Task<Int32> RequestMtuNativeAsync(Int32 requestValue)
        {
            Trace.Message("Request MTU not supported in UWP");
            return Task.FromResult(-1);
        }

        protected override Boolean UpdateConnectionIntervalNative(ConnectionInterval interval)
        {
            Trace.Message("Update Connection Interval not supported in UWP");
            throw new NotImplementedException();
        }
    }
}