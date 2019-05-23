using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.OS;
using DH.BloubulLE.Contracts;
using Java.Util;
using Object = Java.Lang.Object;

namespace DH.BloubulLE
{
    public class Adapter : AdapterBase
    {
        private readonly Api18BleScanCallback _api18ScanCallback;
        private readonly Api21BleScanCallback _api21ScanCallback;
        private readonly BluetoothAdapter _bluetoothAdapter;
        private readonly BluetoothManager _bluetoothManager;

        public Adapter(BluetoothManager bluetoothManager)
        {
            this._bluetoothManager = bluetoothManager;
            this._bluetoothAdapter = bluetoothManager.Adapter;

            this.ConnectedDeviceRegistry = new Dictionary<String, IDevice>();

            // TODO: bonding
            //var bondStatusBroadcastReceiver = new BondStatusBroadcastReceiver();
            //Application.Context.RegisterReceiver(bondStatusBroadcastReceiver,
            //    new IntentFilter(BluetoothDevice.ActionBondStateChanged));

            ////forward events from broadcast receiver
            //bondStatusBroadcastReceiver.BondStateChanged += (s, args) =>
            //{
            //    //DeviceBondStateChanged(this, args);
            //};

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                this._api21ScanCallback = new Api21BleScanCallback(this);
            else
                this._api18ScanCallback = new Api18BleScanCallback(this);
        }

        public override IList<IDevice> ConnectedDevices => this.ConnectedDeviceRegistry.Values.ToList();

        /// <summary>
        /// Used to store all connected devices
        /// </summary>
        public Dictionary<String, IDevice> ConnectedDeviceRegistry { get; }

        protected override Task StartScanningForDevicesNativeAsync(Guid[] serviceUuids, Boolean allowDuplicatesKey,
            CancellationToken scanCancellationToken)
        {
            // clear out the list
            this.DiscoveredDevices.Clear();

            if (Build.VERSION.SdkInt < BuildVersionCodes.Lollipop)
                this.StartScanningOld(serviceUuids);
            else
                this.StartScanningNew(serviceUuids);

            return Task.FromResult(true);
        }

        private void StartScanningOld(Guid[] serviceUuids)
        {
            Boolean hasFilter = serviceUuids?.Any() ?? false;
            UUID[] uuids = null;
            if (hasFilter) uuids = serviceUuids.Select(u => UUID.FromString(u.ToString())).ToArray();
            Trace.Message("Adapter < 21: Starting a scan for devices.");
#pragma warning disable 618
            this._bluetoothAdapter.StartLeScan(uuids, this._api18ScanCallback);
#pragma warning restore 618
        }

        private void StartScanningNew(Guid[] serviceUuids)
        {
            Boolean hasFilter = serviceUuids?.Any() ?? false;
            List<ScanFilter> scanFilters = null;

            if (hasFilter)
            {
                scanFilters = new List<ScanFilter>();
                foreach (Guid serviceUuid in serviceUuids)
                {
                    ScanFilter.Builder sfb = new ScanFilter.Builder();
                    sfb.SetServiceUuid(ParcelUuid.FromString(serviceUuid.ToString()));
                    scanFilters.Add(sfb.Build());
                }
            }

            ScanSettings.Builder ssb = new ScanSettings.Builder();
            ssb.SetScanMode(this.ScanMode.ToNative());
            //ssb.SetCallbackType(ScanCallbackType.AllMatches);

            if (this._bluetoothAdapter.BluetoothLeScanner != null)
            {
                Trace.Message($"Adapter >=21: Starting a scan for devices. ScanMode: {this.ScanMode}");
                if (hasFilter) Trace.Message($"ScanFilters: {String.Join(", ", serviceUuids)}");
                this._bluetoothAdapter.BluetoothLeScanner.StartScan(scanFilters, ssb.Build(), this._api21ScanCallback);
            }
            else
            {
                Trace.Message("Adapter >= 21: Scan failed. Bluetooth is probably off");
            }
        }

        protected override void StopScanNative()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.Lollipop)
            {
                Trace.Message("Adapter < 21: Stopping the scan for devices.");
#pragma warning disable 618
                this._bluetoothAdapter.StopLeScan(this._api18ScanCallback);
#pragma warning restore 618
            }
            else
            {
                Trace.Message("Adapter >= 21: Stopping the scan for devices.");
                this._bluetoothAdapter.BluetoothLeScanner?.StopScan(this._api21ScanCallback);
            }
        }

        protected override Task ConnectToDeviceNativeAsync(IDevice device, ConnectParameters connectParameters,
            CancellationToken cancellationToken)
        {
            ((Device) device).Connect(connectParameters, cancellationToken);
            return Task.CompletedTask;
        }

        protected override void DisconnectDeviceNative(IDevice device)
        {
            //make sure everything is disconnected
            ((Device) device).Disconnect();
        }

        public override async Task<IDevice> ConnectToKnownDeviceAsync(Guid deviceGuid,
            ConnectParameters connectParameters = default(ConnectParameters),
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Byte[] macBytes = deviceGuid.ToByteArray().Skip(10).Take(6).ToArray();
            BluetoothDevice nativeDevice = this._bluetoothAdapter.GetRemoteDevice(macBytes);

            Device device = new Device(this, nativeDevice, null, 0, new Byte[] { });

            await this.ConnectToDeviceAsync(device, connectParameters, cancellationToken);
            return device;
        }

        public override List<IDevice> GetSystemConnectedOrPairedDevices(Guid[] services = null)
        {
            if (services != null)
                Trace.Message(
                    "Caution: GetSystemConnectedDevices does not take into account the 'services' parameter on Android.");

            //add dualMode type too as they are BLE too ;)
            IEnumerable<BluetoothDevice> connectedDevices = this._bluetoothManager.GetConnectedDevices(ProfileType.Gatt)
                .Where(d => d.Type == BluetoothDeviceType.Le || d.Type == BluetoothDeviceType.Dual);

            IEnumerable<BluetoothDevice> bondedDevices = this._bluetoothAdapter.BondedDevices.Where(d =>
                d.Type == BluetoothDeviceType.Le || d.Type == BluetoothDeviceType.Dual);

            return connectedDevices.Union(bondedDevices, new DeviceComparer()).Select(d => new Device(this, d, null, 0))
                .Cast<IDevice>().ToList();
        }

        private class DeviceComparer : IEqualityComparer<BluetoothDevice>
        {
            public Boolean Equals(BluetoothDevice x, BluetoothDevice y)
            {
                return x.Address == y.Address;
            }

            public Int32 GetHashCode(BluetoothDevice obj)
            {
                return obj.GetHashCode();
            }
        }


        public class Api18BleScanCallback : Object, BluetoothAdapter.ILeScanCallback
        {
            private readonly Adapter _adapter;

            public Api18BleScanCallback(Adapter adapter)
            {
                this._adapter = adapter;
            }

            public void OnLeScan(BluetoothDevice bleDevice, Int32 rssi, Byte[] scanRecord)
            {
                Trace.Message("Adapter.LeScanCallback: " + bleDevice.Name);

                this._adapter.HandleDiscoveredDevice(new Device(this._adapter, bleDevice, null, rssi, scanRecord));
            }
        }


        public class Api21BleScanCallback : ScanCallback
        {
            private readonly Adapter _adapter;

            public Api21BleScanCallback(Adapter adapter)
            {
                this._adapter = adapter;
            }

            public override void OnScanFailed(ScanFailure errorCode)
            {
                Trace.Message("Adapter: Scan failed with code {0}", errorCode);
                base.OnScanFailed(errorCode);
            }

            public override void OnScanResult(ScanCallbackType callbackType, ScanResult result)
            {
                base.OnScanResult(callbackType, result);

                /* Might want to transition to parsing the API21+ ScanResult, but sort of a pain for now 
                List<AdvertisementRecord> records = new List<AdvertisementRecord>();
                records.Add(new AdvertisementRecord(AdvertisementRecordType.Flags, BitConverter.GetBytes(result.ScanRecord.AdvertiseFlags)));
                if (!string.IsNullOrEmpty(result.ScanRecord.DeviceName))
                {
                    records.Add(new AdvertisementRecord(AdvertisementRecordType.CompleteLocalName, Encoding.UTF8.GetBytes(result.ScanRecord.DeviceName)));
                }
                for (int i = 0; i < result.ScanRecord.ManufacturerSpecificData.Size(); i++)
                {
                    int key = result.ScanRecord.ManufacturerSpecificData.KeyAt(i);
                    var arr = result.ScanRecord.GetManufacturerSpecificData(key);
                    byte[] data = new byte[arr.Length + 2];
                    BitConverter.GetBytes((ushort)key).CopyTo(data,0);
                    arr.CopyTo(data, 2);
                    records.Add(new AdvertisementRecord(AdvertisementRecordType.ManufacturerSpecificData, data));
                }

                foreach(var uuid in result.ScanRecord.ServiceUuids)
                {
                    records.Add(new AdvertisementRecord(AdvertisementRecordType.UuidsIncomplete128Bit, uuid.Uuid.));
                }

                foreach(var key in result.ScanRecord.ServiceData.Keys)
                {
                    records.Add(new AdvertisementRecord(AdvertisementRecordType.ServiceData, result.ScanRecord.ServiceData));
                }*/

                Device device = new Device(this._adapter, result.Device, null, result.Rssi,
                    result.ScanRecord.GetBytes());

                //Device device;
                //if (result.ScanRecord.ManufacturerSpecificData.Size() > 0)
                //{
                //    int key = result.ScanRecord.ManufacturerSpecificData.KeyAt(0);
                //    byte[] mdata = result.ScanRecord.GetManufacturerSpecificData(key);
                //    byte[] mdataWithKey = new byte[mdata.Length + 2];
                //    BitConverter.GetBytes((ushort)key).CopyTo(mdataWithKey, 0);
                //    mdata.CopyTo(mdataWithKey, 2);
                //    device = new Device(result.Device, null, null, result.Rssi, mdataWithKey);
                //}
                //else
                //{
                //    device = new Device(result.Device, null, null, result.Rssi, new byte[0]);
                //}

                this._adapter.HandleDiscoveredDevice(device);
            }
        }
    }
}