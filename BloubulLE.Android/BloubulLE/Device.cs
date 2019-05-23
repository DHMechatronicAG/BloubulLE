using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.OS;
using DH.BloubulLE.CallbackEventArgs;
using DH.BloubulLE.Contracts;
using DH.BloubulLE.Utils;
using Java.Lang;
using Java.Lang.Reflect;
using Array = System.Array;
using Boolean = System.Boolean;
using Byte = System.Byte;
using Enum = System.Enum;
using Exception = System.Exception;
using Object = System.Object;
using String = System.String;

namespace DH.BloubulLE
{
    public class Device : DeviceBase
    {
        /// <summary>
        /// we also track this because of gogole's weird API. the gatt callback is where
        /// we'll get notified when services are enumerated
        /// </summary>
        private readonly GattCallback _gattCallback;

        /// <summary>
        /// the registration must be disposed to avoid disconnecting after a connection
        /// </summary>
        private CancellationTokenRegistration _connectCancellationTokenRegistration;

        /// <summary>
        /// we have to keep a reference to this because Android's api is weird and requires
        /// the GattServer in order to do nearly anything, including enumerating services
        /// </summary>
        internal BluetoothGatt _gatt;

        public Device(Adapter adapter, BluetoothDevice nativeDevice, BluetoothGatt gatt, Int32 rssi,
            Byte[] advertisementData = null) : base(adapter)
        {
            this.Update(nativeDevice, gatt);
            this.Rssi = rssi;
            this.AdvertisementRecords = ParseScanRecord(advertisementData);
            this._gattCallback = new GattCallback(adapter, this);
        }

        public BluetoothDevice BluetoothDevice { get; private set; }

        public override Object NativeDevice => this.BluetoothDevice;
        internal Boolean IsOperationRequested { get; set; }

        public void Update(BluetoothDevice nativeDevice, BluetoothGatt gatt)
        {
            this._connectCancellationTokenRegistration.Dispose();
            this._connectCancellationTokenRegistration = new CancellationTokenRegistration();

            this.BluetoothDevice = nativeDevice;
            this._gatt = gatt;


            this.Id = this.ParseDeviceId();
            this.Name = this.BluetoothDevice.Name;
        }

        protected override async Task<IEnumerable<IService>> GetServicesNativeAsync()
        {
            if (this._gattCallback == null || this._gatt == null) return Enumerable.Empty<IService>();

            return await TaskBuilder
                .FromEvent<IEnumerable<IService>, EventHandler<ServicesDiscoveredCallbackEventArgs>, EventHandler>(
                    () => this._gatt.DiscoverServices(),
                    (complete, reject) => (sender, args) =>
                    {
                        complete(this._gatt.Services.Select(service =>
                            new Service(service, this._gatt, this._gattCallback, this)));
                    },
                    handler => this._gattCallback.ServicesDiscovered += handler,
                    handler => this._gattCallback.ServicesDiscovered -= handler,
                    reject => (sender, args) =>
                    {
                        reject(new Exception($"Device {this.Name} disconnected while fetching services."));
                    },
                    handler => this._gattCallback.ConnectionInterrupted += handler,
                    handler => this._gattCallback.ConnectionInterrupted -= handler);
        }

        public void Connect(ConnectParameters connectParameters, CancellationToken cancellationToken)
        {
            this.IsOperationRequested = true;

            if (connectParameters.ForceBleTransport)
            {
                this.ConnectToGattForceBleTransportAPI(connectParameters.AutoConnect, cancellationToken);
            }
            else
            {
                BluetoothGatt connectGatt = this.BluetoothDevice.ConnectGatt(Application.Context,
                    connectParameters.AutoConnect, this._gattCallback);
                this._connectCancellationTokenRegistration.Dispose();
                this._connectCancellationTokenRegistration = cancellationToken.Register(() => connectGatt.Disconnect());
            }
        }

        private void ConnectToGattForceBleTransportAPI(Boolean autoconnect, CancellationToken cancellationToken)
        {
            //This parameter is present from API 18 but only public from API 23
            //So reflection is used before API 23
            if (Build.VERSION.SdkInt < BuildVersionCodes.Lollipop)
            {
                //no transport mode before lollipop, it will probably not work... gattCallBackError 133 again alas
                BluetoothGatt connectGatt =
                    this.BluetoothDevice.ConnectGatt(Application.Context, autoconnect, this._gattCallback);
                this._connectCancellationTokenRegistration.Dispose();
                this._connectCancellationTokenRegistration = cancellationToken.Register(() => connectGatt.Disconnect());
            }
            else if (Build.VERSION.SdkInt < BuildVersionCodes.M)
            {
                Method m = this.BluetoothDevice.Class.GetDeclaredMethod("connectGatt", Class.FromType(typeof(Context)),
                    Java.Lang.Boolean.Type, Class.FromType(typeof(BluetoothGattCallback)), Integer.Type);

                Int32 transport =
                    this.BluetoothDevice.Class.GetDeclaredField("TRANSPORT_LE")
                        .GetInt(null); // LE = 2, BREDR = 1, AUTO = 0
                m.Invoke(this.BluetoothDevice, Application.Context, false, this._gattCallback, transport);
            }
            else
            {
                BluetoothGatt connectGatt = this.BluetoothDevice.ConnectGatt(Application.Context, autoconnect,
                    this._gattCallback, BluetoothTransports.Le);
                this._connectCancellationTokenRegistration.Dispose();
                this._connectCancellationTokenRegistration = cancellationToken.Register(() => connectGatt.Disconnect());
            }
        }

        /// <summary>
        /// This method is only called by a user triggered disconnect.
        /// A user will first trigger _gatt.disconnect -> which in turn will trigger _gatt.Close() via the gattCallback
        /// </summary>
        public void Disconnect()
        {
            if (this._gatt != null)
            {
                this.IsOperationRequested = true;

                this.ClearServices();

                this._gatt.Disconnect();
            }
            else
            {
                Trace.Message("[Warning]: Can't disconnect {0}. Gatt is null.", this.Name);
            }
        }

        /// <summary>
        /// CloseGatt is called by the gattCallback in case of user disconnect or a disconnect by signal loss or a connection error.
        /// Cleares all cached services.
        /// </summary>
        public void CloseGatt()
        {
            this._gatt?.Close();
            this._gatt = null;

            // ClossGatt might will get called on signal loss without Disconnect being called we have to make sure we clear the services
            // Clear services & characteristics otherwise we will get gatt operation return FALSE when connecting to the same IDevice instace at a later time
            this.ClearServices();
        }

        protected override DeviceState GetState()
        {
            BluetoothManager manager =
                (BluetoothManager) Application.Context.GetSystemService(Context.BluetoothService);
            ProfileState state = manager.GetConnectionState(this.BluetoothDevice, ProfileType.Gatt);

            switch (state)
            {
                case ProfileState.Connected:
                    // if the device does not have a gatt instance we can't use it in the app, so we need to explicitly be able to connect it
                    // even if the profile state is connected
                    return this._gatt != null ? DeviceState.Connected : DeviceState.Limited;

                case ProfileState.Connecting:
                    return DeviceState.Connecting;

                case ProfileState.Disconnected:
                case ProfileState.Disconnecting:
                default:
                    return DeviceState.Disconnected;
            }
        }

        private Guid ParseDeviceId()
        {
            Byte[] deviceGuid = new Byte[16];
            String macWithoutColons = this.BluetoothDevice.Address.Replace(":", "");
            Byte[] macBytes = Enumerable.Range(0, macWithoutColons.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(macWithoutColons.Substring(x, 2), 16))
                .ToArray();
            macBytes.CopyTo(deviceGuid, 10);
            return new Guid(deviceGuid);
        }

        public static List<AdvertisementRecord> ParseScanRecord(Byte[] scanRecord)
        {
            List<AdvertisementRecord> records = new List<AdvertisementRecord>();

            if (scanRecord == null)
                return records;

            Int32 index = 0;
            while (index < scanRecord.Length)
            {
                Byte length = scanRecord[index++];
                //Done once we run out of records 
                // 1 byte for type and length-1 bytes for data
                if (length == 0) break;

                Int32 type = scanRecord[index];
                //Done if our record isn't a valid type
                if (type == 0) break;

                if (!Enum.IsDefined(typeof(AdvertisementRecordType), type))
                {
                    Trace.Message("Advertisment record type not defined: {0}", type);
                    break;
                }

                //data length is length -1 because type takes the first byte
                Byte[] data = new Byte[length - 1];
                Array.Copy(scanRecord, index + 1, data, 0, length - 1);

                // don't forget that data is little endian so reverse
                // Supplement to Bluetooth Core Specification 1
                // NOTE: all relevant devices are already little endian, so this is not necessary for any type except UUIDs
                //var record = new AdvertisementRecord((AdvertisementRecordType)type, data.Reverse().ToArray());

                switch ((AdvertisementRecordType) type)
                {
                    case AdvertisementRecordType.ServiceDataUuid32Bit:
                    case AdvertisementRecordType.SsUuids128Bit:
                    case AdvertisementRecordType.SsUuids16Bit:
                    case AdvertisementRecordType.SsUuids32Bit:
                    case AdvertisementRecordType.UuidCom32Bit:
                    case AdvertisementRecordType.UuidsComplete128Bit:
                    case AdvertisementRecordType.UuidsComplete16Bit:
                    case AdvertisementRecordType.UuidsIncomple16Bit:
                    case AdvertisementRecordType.UuidsIncomplete128Bit:
                        Array.Reverse(data);
                        break;
                }

                AdvertisementRecord record = new AdvertisementRecord((AdvertisementRecordType) type, data);

                Trace.Message(record.ToString());

                records.Add(record);

                //Advance
                index += length;
            }

            return records;
        }

        public override async Task<Boolean> UpdateRssiAsync()
        {
            if (this._gatt == null || this._gattCallback == null)
            {
                Trace.Message(
                    "You can't read the RSSI value for disconnected devices except on discovery on Android. Device is {0}",
                    this.State);
                return false;
            }

            return await TaskBuilder.FromEvent<Boolean, EventHandler<RssiReadCallbackEventArgs>, EventHandler>(
                () => this._gatt.ReadRemoteRssi(),
                (complete, reject) => (sender, args) =>
                {
                    if (args.Error == null)
                    {
                        Trace.Message("Read RSSI for {0} {1}: {2}", this.Id, this.Name, args.Rssi);
                        this.Rssi = args.Rssi;
                        complete(true);
                    }
                    else
                    {
                        Trace.Message($"Failed to read RSSI for device {this.Id}-{this.Name}. {args.Error.Message}");
                        complete(false);
                    }
                },
                handler => this._gattCallback.RemoteRssiRead += handler,
                handler => this._gattCallback.RemoteRssiRead -= handler,
                reject => (sender, args) =>
                {
                    reject(new Exception($"Device {this.Name} disconnected while updating rssi."));
                },
                handler => this._gattCallback.ConnectionInterrupted += handler,
                handler => this._gattCallback.ConnectionInterrupted -= handler);
        }

        protected override async Task<Int32> RequestMtuNativeAsync(Int32 requestValue)
        {
            if (this._gatt == null || this._gattCallback == null)
            {
                Trace.Message("You can't request a MTU for disconnected devices. Device is {0}", this.State);
                return -1;
            }

            if (Build.VERSION.SdkInt < BuildVersionCodes.Lollipop)
            {
                Trace.Message("Request MTU not supported in this Android API level");
                return -1;
            }

            return await TaskBuilder.FromEvent<Int32, EventHandler<MtuRequestCallbackEventArgs>, EventHandler>(
                () => { this._gatt.RequestMtu(requestValue); },
                (complete, reject) => (sender, args) =>
                {
                    if (args.Error != null)
                    {
                        Trace.Message(
                            $"Failed to request MTU ({requestValue}) for device {this.Id}-{this.Name}. {args.Error.Message}");
                        reject(new Exception($"Request MTU error: {args.Error.Message}"));
                    }
                    else
                    {
                        complete(args.Mtu);
                    }
                },
                handler => this._gattCallback.MtuRequested += handler,
                handler => this._gattCallback.MtuRequested -= handler,
                reject => (sender, args) =>
                {
                    reject(new Exception($"Device {this.Name} disconnected while requesting MTU."));
                },
                handler => this._gattCallback.ConnectionInterrupted += handler,
                handler => this._gattCallback.ConnectionInterrupted -= handler
            );
        }

        protected override Boolean UpdateConnectionIntervalNative(ConnectionInterval interval)
        {
            if (this._gatt == null || this._gattCallback == null)
            {
                Trace.Message("You can't update a connection interval for disconnected devices. Device is {0}",
                    this.State);
                return false;
            }

            if (Build.VERSION.SdkInt < BuildVersionCodes.Lollipop)
            {
                Trace.Message("Update connection interval paramter in this Android API level");
                return false;
            }

            try
            {
                // map to android gattConnectionPriorities
                // https://developer.android.com/reference/android/bluetooth/BluetoothGatt.html#CONNECTION_PRIORITY_BALANCED
                return this._gatt.RequestConnectionPriority((GattConnectionPriority) (Int32) interval);
            }
            catch (Exception ex)
            {
                throw new Exception($"Update Connection Interval fails with error. {ex.Message}");
            }
        }
    }
}