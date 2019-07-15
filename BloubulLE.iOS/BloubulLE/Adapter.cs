using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreBluetooth;
using DH.BloubulLE.Contracts;
using Foundation;

namespace DH.BloubulLE
{
    public class Adapter : AdapterBase
    {
        private readonly CBCentralManager _centralManager;

        private readonly IDictionary<String, IDevice> _deviceConnectionRegistry =
            new ConcurrentDictionary<String, IDevice>();

        /// <summary>
        /// Registry used to store device instances for pending operations : disconnect
        /// Helps to detect connection lost events.
        /// </summary>
        private readonly IDictionary<String, IDevice> _deviceOperationRegistry =
            new ConcurrentDictionary<String, IDevice>();

        private readonly AutoResetEvent _stateChanged = new AutoResetEvent(false);


        public Adapter(CBCentralManager centralManager)
        {
            this._centralManager = centralManager;
            this._centralManager.DiscoveredPeripheral += (sender, e) =>
            {
                Trace.Message("DiscoveredPeripheral: {0}, Id: {1}", e.Peripheral.Name, e.Peripheral.Identifier);
                String name = e.Peripheral.Name;
                if (e.AdvertisementData.ContainsKey(CBAdvertisement.DataLocalNameKey))
                    // iOS caches the peripheral name, so it can become stale (if changing)
                    // keep track of the local name key manually
                    name = ((NSString) e.AdvertisementData.ValueForKey(CBAdvertisement.DataLocalNameKey)).ToString();

                Device device = new Device(this, e.Peripheral, this._centralManager, name, e.RSSI.Int32Value,
                    ParseAdvertismentData(e.AdvertisementData));
                this.HandleDiscoveredDevice(device);
            };

            this._centralManager.UpdatedState += (sender, e) =>
            {
                Trace.Message("UpdatedState: {0}", this._centralManager.State);
                this._stateChanged.Set();

                //handle PoweredOff state
                //notify subscribers about disconnection
                if (this._centralManager.State == CBCentralManagerState.PoweredOff)
                    foreach (IDevice device in this._deviceConnectionRegistry.Values.ToList())
                    {
                        this._deviceConnectionRegistry.Remove(device.Id.ToString());
                        ((Device) device).ClearServices();
                        this.HandleDisconnectedDevice(false, device);
                    }
            };

            this._centralManager.ConnectedPeripheral += (sender, e) =>
            {
                Trace.Message("ConnectedPeripherial: {0}", e.Peripheral.Name);

                // when a peripheral gets connected, add that peripheral to our running list of connected peripherals
                String guid = ParseDeviceGuid(e.Peripheral).ToString();

                IDevice device;
                if (this._deviceOperationRegistry.TryGetValue(guid, out device))
                {
                    this._deviceOperationRegistry.Remove(guid);
                    ((Device) device).Update(e.Peripheral);
                }
                else
                {
                    Trace.Message("Device not found in operation registry. Creating a new one.");
                    device = new Device(this, e.Peripheral, this._centralManager);
                }

                this._deviceConnectionRegistry[guid] = device;
                this.HandleConnectedDevice(device);
            };

            this._centralManager.DisconnectedPeripheral += (sender, e) =>
            {
                if (e.Error != null)
                    Trace.Message("Disconnect error {0} {1} {2}", e.Error.Code, e.Error.Description, e.Error.Domain);

                // when a peripheral disconnects, remove it from our running list.
                Guid id = ParseDeviceGuid(e.Peripheral);
                String stringId = id.ToString();
                IDevice foundDevice;

                // normal disconnect (requested by user)
                Boolean isNormalDisconnect = this._deviceOperationRegistry.TryGetValue(stringId, out foundDevice);
                if (isNormalDisconnect) this._deviceOperationRegistry.Remove(stringId);

                // check if it is a peripheral disconnection, which would be treated as normal
                if (e.Error != null && e.Error.Code == 7 && e.Error.Domain == "CBErrorDomain")
                    isNormalDisconnect = true;

                // remove from connected devices
                if (this._deviceConnectionRegistry.TryGetValue(stringId, out foundDevice))
                    this._deviceConnectionRegistry.Remove(stringId);

                foundDevice = foundDevice ?? new Device(this, e.Peripheral, this._centralManager);

                //make sure all cached services are cleared this will also clear characteristics and descriptors implicitly
                ((Device) foundDevice).ClearServices();

                this.HandleDisconnectedDevice(isNormalDisconnect, foundDevice);
            };

            this._centralManager.FailedToConnectPeripheral +=
                (sender, e) =>
                {
                    Guid id = ParseDeviceGuid(e.Peripheral);
                    String stringId = id.ToString();
                    IDevice foundDevice;

                    // remove instance from registry
                    if (this._deviceOperationRegistry.TryGetValue(stringId, out foundDevice))
                        this._deviceOperationRegistry.Remove(stringId);

                    foundDevice = foundDevice ?? new Device(this, e.Peripheral, this._centralManager);

                    this.HandleConnectionFail(foundDevice, e.Error.Description);
                };
        }

        public override IList<IDevice> ConnectedDevices => this._deviceConnectionRegistry.Values.ToList();

        protected override async Task<bool> StartScanningForDevicesNativeAsync(Guid[] serviceUuids,
            Boolean allowDuplicatesKey, CancellationToken scanCancellationToken)
        {
            // Wait for the PoweredOn state
            await this.WaitForState(CBCentralManagerState.PoweredOn, scanCancellationToken).ConfigureAwait(false);

            if (scanCancellationToken.IsCancellationRequested)
                throw new TaskCanceledException("StartScanningForDevicesNativeAsync cancelled");

            Trace.Message("Adapter: Starting a scan for devices.");

            CBUUID[] serviceCbuuids = null;
            if (serviceUuids != null && serviceUuids.Any())
            {
                serviceCbuuids = serviceUuids.Select(u => CBUUID.FromString(u.ToString())).ToArray();
                Trace.Message("Adapter: Scanning for " + serviceCbuuids.First());
            }

            this.DiscoveredDevices.Clear();
            this._centralManager.ScanForPeripherals(serviceCbuuids,
                new PeripheralScanningOptions {AllowDuplicatesKey = allowDuplicatesKey});

            return true;
        }

        protected override void DisconnectDeviceNative(IDevice device)
        {
            this._deviceOperationRegistry[device.Id.ToString()] = device;
            this._centralManager.CancelPeripheralConnection(device.NativeDevice as CBPeripheral);
        }

        protected override void StopScanNative()
        {
            this._centralManager.StopScan();
        }

        protected override Task ConnectToDeviceNativeAsync(IDevice device, ConnectParameters connectParameters,
            CancellationToken cancellationToken)
        {
            if (connectParameters.AutoConnect) Trace.Message("Warning: Autoconnect is not supported in iOS");

            this._deviceOperationRegistry[device.Id.ToString()] = device;

            this._centralManager.ConnectPeripheral(device.NativeDevice as CBPeripheral,
                new PeripheralConnectionOptions());

            // this is dirty: We should not assume, AdapterBase is doing the cleanup for us...
            // move ConnectToDeviceAsync() code to native implementations.
            cancellationToken.Register(() =>
            {
                Trace.Message("Canceling the connect attempt");
                this._centralManager.CancelPeripheralConnection(device.NativeDevice as CBPeripheral);
            });

            return Task.FromResult(true);
        }

        private static Guid ParseDeviceGuid(CBPeripheral peripherial)
        {
            return Guid.ParseExact(peripherial.Identifier.AsString(), "d");
        }

        /// <summary>
        /// Connects to known device async.
        /// https://developer.apple.com/library/ios/documentation/NetworkingInternetWeb/Conceptual/CoreBluetooth_concepts/BestPracticesForInteractingWithARemotePeripheralDevice/BestPracticesForInteractingWithARemotePeripheralDevice.html
        /// </summary>
        /// <returns>The to known device async.</returns>
        /// <param name="deviceGuid">Device GUID.</param>
        public override async Task<IDevice> ConnectToKnownDeviceAsync(Guid deviceGuid,
            ConnectParameters connectParameters = default(ConnectParameters),
            CancellationToken cancellationToken = default(CancellationToken))
        {
            // Wait for the PoweredOn state
            await this.WaitForState(CBCentralManagerState.PoweredOn, cancellationToken, true);

            if (cancellationToken.IsCancellationRequested)
                throw new TaskCanceledException("ConnectToKnownDeviceAsync cancelled");

            //FYI attempted to use tobyte array insetead of string but there was a problem with byte ordering Guid->NSUui
            NSUuid uuid = new NSUuid(deviceGuid.ToString());

            Trace.Message($"[Adapter] Attempting connection to {uuid}");

            CBPeripheral[] peripherials = this._centralManager.RetrievePeripheralsWithIdentifiers(uuid);
            CBPeripheral peripherial = peripherials.SingleOrDefault();

            if (peripherial == null)
            {
                CBPeripheral[] systemPeripherials = this._centralManager.RetrieveConnectedPeripherals(new CBUUID[0]);

                CBUUID cbuuid = CBUUID.FromNSUuid(uuid);
                peripherial = systemPeripherials.SingleOrDefault(p => p.UUID.Equals(cbuuid));

                if (peripherial == null)
                    throw new Exception($"[Adapter] Device {deviceGuid} not found.");
            }


            Device device = new Device(this, peripherial, this._centralManager, peripherial.Name,
                peripherial.RSSI?.Int32Value ?? 0, new List<AdvertisementRecord>());

            await this.ConnectToDeviceAsync(device, connectParameters, cancellationToken);
            return device;
        }

        public override List<IDevice> GetSystemConnectedOrPairedDevices(Guid[] services = null)
        {
            CBUUID[] serviceUuids = null;
            if (services != null) serviceUuids = services.Select(guid => CBUUID.FromString(guid.ToString())).ToArray();

            CBPeripheral[] nativeDevices = this._centralManager.RetrieveConnectedPeripherals(serviceUuids);

            return nativeDevices.Select(d => new Device(this, d, this._centralManager)).Cast<IDevice>().ToList();
        }

        private async Task WaitForState(CBCentralManagerState state, CancellationToken cancellationToken,
            Boolean configureAwait = false)
        {
            Trace.Message("Adapter: Waiting for state: " + state);

            while (this._centralManager.State != state && !cancellationToken.IsCancellationRequested)
                await Task.Run(() => this._stateChanged.WaitOne(2000), cancellationToken)
                    .ConfigureAwait(configureAwait);
        }

        private static Boolean ContainsDevice(IEnumerable<IDevice> list, CBPeripheral device)
        {
            return list.Any(d => Guid.ParseExact(device.Identifier.AsString(), "d") == d.Id);
        }

        public static List<AdvertisementRecord> ParseAdvertismentData(NSDictionary advertisementData)
        {
            List<AdvertisementRecord> records = new List<AdvertisementRecord>();

            /*var keys = new List<NSString>
            {
                CBAdvertisement.DataLocalNameKey,
                CBAdvertisement.DataManufacturerDataKey, 
                CBAdvertisement.DataOverflowServiceUUIDsKey, //ToDo ??which one is this according to ble spec
                CBAdvertisement.DataServiceDataKey, 
                CBAdvertisement.DataServiceUUIDsKey,
                CBAdvertisement.DataSolicitedServiceUUIDsKey,
                CBAdvertisement.DataTxPowerLevelKey
            };*/

            foreach (NSObject o in advertisementData.Keys)
            {
                NSString key = (NSString) o;
                if (key == CBAdvertisement.DataLocalNameKey)
                {
                    records.Add(new AdvertisementRecord(AdvertisementRecordType.CompleteLocalName,
                        NSData.FromString(advertisementData.ObjectForKey(key) as NSString).ToArray()));
                }
                else if (key == CBAdvertisement.DataManufacturerDataKey)
                {
                    Byte[] arr = ((NSData) advertisementData.ObjectForKey(key)).ToArray();
                    records.Add(new AdvertisementRecord(AdvertisementRecordType.ManufacturerSpecificData, arr));
                }
                else if (key == CBAdvertisement.DataServiceUUIDsKey)
                {
                    NSArray array = (NSArray) advertisementData.ObjectForKey(key);

                    List<NSData> dataList = new List<NSData>();
                    for (nuint i = 0; i < array.Count; i++)
                    {
                        CBUUID cbuuid = array.GetItem<CBUUID>(i);
                        dataList.Add(cbuuid.Data);
                    }

                    records.Add(new AdvertisementRecord(AdvertisementRecordType.UuidsComplete128Bit,
                        dataList.SelectMany(d => d.ToArray()).ToArray()));
                }
                else if (key == CBAdvertisement.DataTxPowerLevelKey)
                {
                    //iOS stores TxPower as NSNumber. Get int value of number and convert it into a signed Byte
                    //TxPower has a range from -100 to 20 which can fit into a single signed byte (-128 to 127)
                    SByte byteValue = Convert.ToSByte(((NSNumber) advertisementData.ObjectForKey(key)).Int32Value);
                    //add our signed byte to a new byte array and return it (same parsed value as android returns)
                    Byte[] arr = {(Byte) byteValue};
                    records.Add(new AdvertisementRecord(AdvertisementRecordType.TxPowerLevel, arr));
                }
                else if (key == CBAdvertisement.DataServiceDataKey)
                {
                    //Service data from CoreBluetooth is returned as a key/value dictionary with the key being
                    //the service uuid (CBUUID) and the value being the NSData (bytes) of the service
                    //This is where you'll find eddystone and other service specific data
                    NSDictionary serviceDict = (NSDictionary) advertisementData.ObjectForKey(key);
                    //There can be multiple services returned in the dictionary, so loop through them
                    foreach (CBUUID dKey in serviceDict.Keys)
                    {
                        //Get the service key in bytes (from NSData)
                        Byte[] keyAsData = dKey.Data.ToArray();

                        //Service UUID's are read backwards (little endian) according to specs, 
                        //CoreBluetooth returns the service UUIDs as Big Endian
                        //but to match the raw service data returned from Android we need to reverse it back
                        //Note haven't tested it yet on 128bit service UUID's, but should work
                        Array.Reverse(keyAsData);

                        //The service data under this key can just be turned into an arra
                        NSData data = (NSData) serviceDict.ObjectForKey(dKey);
                        Byte[] valueAsData = data.Length > 0 ? data.ToArray() : new Byte[0];

                        //Now we append the key and value data and return that so that our parsing matches the raw
                        //byte value returned from the Android library (which matches the raw bytes from the device)
                        Byte[] arr = new Byte[keyAsData.Length + valueAsData.Length];
                        Buffer.BlockCopy(keyAsData, 0, arr, 0, keyAsData.Length);
                        Buffer.BlockCopy(valueAsData, 0, arr, keyAsData.Length, valueAsData.Length);

                        records.Add(new AdvertisementRecord(AdvertisementRecordType.ServiceData, arr));
                    }
                }
                else if (key == CBAdvertisement.IsConnectable)
                {
                    // A Boolean value that indicates whether the advertising event type is connectable.
                    // The value for this key is an NSNumber object. You can use this value to determine whether a peripheral is connectable at a particular moment.
                    records.Add(new AdvertisementRecord(AdvertisementRecordType.IsConnectable,
                        new[] {((NSNumber) advertisementData.ObjectForKey(key)).ByteValue}));
                }
                else
                {
                    Trace.Message(
                        "Parsing Advertisement: Ignoring Advertisement entry for key {0}, since we don't know how to parse it yet. Maybe you can open a Pull Request and implement it ;)",
                        key.ToString());
                }
            }

            return records;
        }
    }
}