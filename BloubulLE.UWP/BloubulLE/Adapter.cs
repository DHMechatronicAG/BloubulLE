using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using DH.BloubulLE.Contracts;
using Microsoft.Toolkit.Uwp.Connectivity;

namespace DH.BloubulLE
{
    public class Adapter : AdapterBase
    {
        private BluetoothLEAdvertisementWatcher _BleWatcher;
        private BluetoothLEHelper _bluetoothHelper;

        /// <summary>
        /// Needed to check for scanned devices so that duplicated don't get
        /// added due to race conditions
        /// </summary>
        private IList<UInt64> _prevScannedDevices;


        public Adapter(BluetoothLEHelper bluetoothHelper)
        {
            this._bluetoothHelper = bluetoothHelper;
            this.ConnectedDeviceRegistry = new Dictionary<String, IDevice>();
        }

        /// <summary>
        /// Used to store all connected devices
        /// </summary>
        public IDictionary<String, IDevice> ConnectedDeviceRegistry { get; }

        public override IList<IDevice> ConnectedDevices => this.ConnectedDeviceRegistry.Values.ToList();

        protected override Task StartScanningForDevicesNativeAsync(Guid[] serviceUuids, Boolean allowDuplicatesKey,
            CancellationToken scanCancellationToken)
        {
            Boolean hasFilter = serviceUuids?.Any() ?? false;
            this.DiscoveredDevices.Clear();
            this._BleWatcher = new BluetoothLEAdvertisementWatcher();
            this._BleWatcher.ScanningMode = BluetoothLEScanningMode.Active;
            this._prevScannedDevices = new List<UInt64>();
            Trace.Message("Starting a scan for devices.");
            if (hasFilter)
            {
                //adds filter to native scanner if serviceUuids are specified
                foreach (Guid uuid in serviceUuids)
                    this._BleWatcher.AdvertisementFilter.Advertisement.ServiceUuids.Add(uuid);
                Trace.Message($"ScanFilters: {String.Join(", ", serviceUuids)}");
            }

            //don't allow duplicates except for testing, results in multiple versions
            //of the same device being found
            if (allowDuplicatesKey)
                this._BleWatcher.Received += this.DeviceFoundAsyncDuplicate;
            else
                this._BleWatcher.Received += this.DeviceFoundAsync;
            this._BleWatcher.Start();
            return Task.FromResult(true);
        }

        protected override void StopScanNative()
        {
            Trace.Message("Stopping the scan for devices");
            this._BleWatcher.Stop();
            this._BleWatcher = null;
        }

        protected override async Task ConnectToDeviceNativeAsync(IDevice device, ConnectParameters connectParameters,
            CancellationToken cancellationToken)
        {
            Trace.Message($"Connecting to device with ID:  {device.Id.ToString()}");

            ObservableBluetoothLEDevice nativeDevice = device.NativeDevice as ObservableBluetoothLEDevice;
            if (nativeDevice == null)
                return;

            Device uwpDevice = (Device) device;
            uwpDevice.ConnectionStatusChanged += this.Device_ConnectionStatusChanged;

            await nativeDevice.ConnectAsync();

            if (!this.ConnectedDeviceRegistry.ContainsKey(uwpDevice.Id.ToString()))
                this.ConnectedDeviceRegistry.Add(uwpDevice.Id.ToString(), device);
        }

        private void Device_ConnectionStatusChanged(Device device, BluetoothConnectionStatus status)
        {
            if (status == BluetoothConnectionStatus.Connected)
                this.HandleConnectedDevice(device);
            else
                this.HandleDisconnectedDevice(true, device);
        }

        protected override void DisconnectDeviceNative(IDevice device)
        {
            // Windows doesn't support disconnecting, so currently just dispose of the device
            Trace.Message($"Disconnected from device with ID:  {device.Id.ToString()}");
            this.ConnectedDeviceRegistry.Remove(device.Id.ToString());
        }

        public override async Task<IDevice> ConnectToKnownDeviceAsync(Guid deviceGuid,
            ConnectParameters connectParameters, CancellationToken cancellationToken)
        {
            //convert GUID to string and take last 12 characters as MAC address
            String guidString = deviceGuid.ToString("N").Substring(20);
            UInt64 bluetoothAddr = Convert.ToUInt64(guidString, 16);
            BluetoothLEDevice nativeDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddr);
            Device currDevice = new Device(this, nativeDevice, 0, guidString);
            await this.ConnectToDeviceAsync(currDevice);
            return currDevice;
        }

        public override List<IDevice> GetSystemConnectedOrPairedDevices(Guid[] services = null)
        {
            //currently no way to retrieve paired and connected devices on windows without using an
            //async method. 
            Trace.Message("Returning devices connected by this app only");
            return (List<IDevice>) this.ConnectedDevices;
        }

        /// <summary>
        /// Parses a given advertisement for various stored properties
        /// Currently only parses the manufacturer specific data
        /// </summary>
        /// <param name="adv">The advertisement to parse</param>
        /// <returns>List of generic advertisement records</returns>
        public static List<AdvertisementRecord> ParseAdvertisementData(BluetoothLEAdvertisement adv)
        {
            IList<BluetoothLEAdvertisementDataSection> advList = adv.DataSections;
            List<AdvertisementRecord> records = new List<AdvertisementRecord>();
            foreach (BluetoothLEAdvertisementDataSection data in advList)
            {
                Byte type = data.DataType;
                if (type == BluetoothLEAdvertisementDataTypes.ManufacturerSpecificData)
                    records.Add(new AdvertisementRecord(AdvertisementRecordType.ManufacturerSpecificData,
                        data.Data.ToArray()));
                //TODO: add more advertisement record types to parse
            }

            return records;
        }

        /// <summary>
        /// Handler for devices found when duplicates are not allowed
        /// </summary>
        /// <param name="watcher">The bluetooth advertisement watcher currently being used</param>
        /// <param name="btAdv">The advertisement recieved by the watcher</param>
        private async void DeviceFoundAsync(BluetoothLEAdvertisementWatcher watcher,
            BluetoothLEAdvertisementReceivedEventArgs btAdv)
        {
            //check if the device was already found before calling generic handler
            //ensures that no device is mistakenly added twice
            if (!this._prevScannedDevices.Contains(btAdv.BluetoothAddress))
            {
                this._prevScannedDevices.Add(btAdv.BluetoothAddress);
                BluetoothLEDevice currDevice =
                    await BluetoothLEDevice.FromBluetoothAddressAsync(btAdv.BluetoothAddress);
                if (currDevice != null) //make sure advertisement bluetooth address actually returns a device
                {
                    Device device = new Device(this, currDevice, btAdv.RawSignalStrengthInDBm,
                        btAdv.BluetoothAddress.ToString(), ParseAdvertisementData(btAdv.Advertisement));
                    Trace.Message("DiscoveredPeripheral: {0} Id: {1}", device.Name, device.Id);
                    this.HandleDiscoveredDevice(device);
                }
            }
        }

        /// <summary>
        /// Handler for devices found when duplicates are allowed
        /// </summary>
        /// <param name="watcher">The bluetooth advertisement watcher currently being used</param>
        /// <param name="btAdv">The advertisement recieved by the watcher</param>
        private async void DeviceFoundAsyncDuplicate(BluetoothLEAdvertisementWatcher watcher,
            BluetoothLEAdvertisementReceivedEventArgs btAdv)
        {
            BluetoothLEDevice currDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(btAdv.BluetoothAddress);
            if (currDevice != null)
            {
                Device device = new Device(this, currDevice, btAdv.RawSignalStrengthInDBm,
                    btAdv.BluetoothAddress.ToString(), ParseAdvertisementData(btAdv.Advertisement));
                Trace.Message("DiscoveredPeripheral: {0} Id: {1}", device.Name, device.Id);
                this.HandleDiscoveredDevice(device);
            }
        }
    }
}