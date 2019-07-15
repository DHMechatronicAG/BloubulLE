using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DH.BloubulLE.Contracts;
using DH.BloubulLE.EventArgs;
using DH.BloubulLE.Exceptions;
using DH.BloubulLE.Utils;

namespace DH.BloubulLE
{
    public abstract class AdapterBase : IAdapter
    {
        private readonly IList<IDevice> _discoveredDevices;
        private Func<IDevice, Boolean> _currentScanDeviceFilter;
        private volatile Boolean _isScanning;
        private CancellationTokenSource _scanCancellationTokenSource;

        protected AdapterBase()
        {
            this._discoveredDevices = new List<IDevice>();
        }

        public event EventHandler<DeviceEventArgs> DeviceAdvertised = delegate { };
        public event EventHandler<DeviceEventArgs> DeviceDiscovered = delegate { };
        public event EventHandler<DeviceEventArgs> DeviceConnected = delegate { };
        public event EventHandler<DeviceEventArgs> DeviceDisconnected = delegate { };
        public event EventHandler<DeviceErrorEventArgs> DeviceConnectionLost = delegate { };
        public event EventHandler ScanTimeoutElapsed = delegate { };

        public Boolean IsScanning
        {
            get => this._isScanning;
            private set => this._isScanning = value;
        }

        public Int32 ScanTimeout { get; set; } = 10000;
        public ScanMode ScanMode { get; set; } = ScanMode.LowPower;

        public virtual IList<IDevice> DiscoveredDevices => this._discoveredDevices;

        public abstract IList<IDevice> ConnectedDevices { get; }

        public async Task<bool> StartScanningForDevicesAsync(Guid[] serviceUuids = null,
            Func<IDevice, Boolean> deviceFilter = null, Boolean allowDuplicatesKey = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (this.IsScanning)
            {
                Trace.Message("Adapter: Already scanning!");
                return true;
            }

            this.IsScanning = true;
            serviceUuids = serviceUuids ?? new Guid[0];
            this._currentScanDeviceFilter = deviceFilter ?? (d => true);
            this._scanCancellationTokenSource = new CancellationTokenSource();

            try
            {
                using (cancellationToken.Register(() => this._scanCancellationTokenSource?.Cancel()))
                {
                    bool tResult = await this.StartScanningForDevicesNativeAsync(serviceUuids, allowDuplicatesKey,
                        this._scanCancellationTokenSource.Token);

                    // If the scan failed to start, we dont need to wait around for nothing
                    if (tResult) { 
                        await Task.Delay(this.ScanTimeout, this._scanCancellationTokenSource.Token);
                        Trace.Message("Adapter: Scan timeout has elapsed.");
                    }

                    this.CleanupScan();
                    this.ScanTimeoutElapsed(this, new System.EventArgs());

                    return tResult;
                }
            }
            catch (TaskCanceledException)
            {
                this.CleanupScan();
                Trace.Message("Adapter: Scan was cancelled.");

                return true;
            }

            return false;
        }

        public Task StopScanningForDevicesAsync()
        {
            if (this._scanCancellationTokenSource != null && !this._scanCancellationTokenSource.IsCancellationRequested)
                this._scanCancellationTokenSource.Cancel();
            else
                Trace.Message("Adapter: Already cancelled scan.");

            return Task.FromResult(0);
        }

        public async Task ConnectToDeviceAsync(IDevice device,
            ConnectParameters connectParameters = default(ConnectParameters),
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            if (device.State == DeviceState.Connected)
                return;

            using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                await TaskBuilder.FromEvent<Boolean, EventHandler<DeviceEventArgs>, EventHandler<DeviceErrorEventArgs>>(
                    () => { this.ConnectToDeviceNativeAsync(device, connectParameters, cts.Token); },
                    (complete, reject) => (sender, args) =>
                    {
                        if (args.Device.Id == device.Id)
                        {
                            Trace.Message("ConnectToDeviceAsync Connected: {0} {1}", args.Device.Id, args.Device.Name);
                            complete(true);
                        }
                    },
                    handler => this.DeviceConnected += handler,
                    handler => this.DeviceConnected -= handler,
                    reject => (sender, args) =>
                    {
                        if (args.Device?.Id == device.Id)
                        {
                            Trace.Message("ConnectAsync Error: {0} {1}", args.Device?.Id, args.Device?.Name);
                            reject(new DeviceConnectionException((Guid) args.Device?.Id, args.Device?.Name,
                                args.ErrorMessage));
                        }
                    },
                    handler => this.DeviceConnectionError += handler,
                    handler => this.DeviceConnectionError -= handler,
                    cts.Token);
            }
        }

        public Task DisconnectDeviceAsync(IDevice device)
        {
            if (!this.ConnectedDevices.Contains(device))
            {
                Trace.Message("Disconnect async: device {0} not in the list of connected devices.", device.Name);
                return Task.FromResult(false);
            }

            return TaskBuilder.FromEvent<Boolean, EventHandler<DeviceEventArgs>, EventHandler<DeviceErrorEventArgs>>(
                () => this.DisconnectDeviceNative(device),
                (complete, reject) => (sender, args) =>
                {
                    if (args.Device.Id == device.Id)
                    {
                        Trace.Message("DisconnectAsync Disconnected: {0} {1}", args.Device.Id, args.Device.Name);
                        complete(true);
                    }
                },
                handler => this.DeviceDisconnected += handler,
                handler => this.DeviceDisconnected -= handler,
                reject => (sender, args) =>
                {
                    if (args.Device.Id == device.Id)
                    {
                        Trace.Message("DisconnectAsync", "Disconnect Error: {0} {1}", args.Device?.Id,
                            args.Device?.Name);
                        reject(new Exception("Disconnect operation exception"));
                    }
                },
                handler => this.DeviceConnectionError += handler,
                handler => this.DeviceConnectionError -= handler);
        }

        public abstract Task<IDevice> ConnectToKnownDeviceAsync(Guid deviceGuid,
            ConnectParameters connectParameters = default(ConnectParameters),
            CancellationToken cancellationToken = default(CancellationToken));

        public abstract List<IDevice> GetSystemConnectedOrPairedDevices(Guid[] services = null);
        public event EventHandler<DeviceErrorEventArgs> DeviceConnectionError = delegate { };

        private void CleanupScan()
        {
            Trace.Message("Adapter: Stopping the scan for devices.");
            this.StopScanNative();

            if (this._scanCancellationTokenSource != null)
            {
                this._scanCancellationTokenSource.Dispose();
                this._scanCancellationTokenSource = null;
            }

            this.IsScanning = false;
        }

        public void HandleDiscoveredDevice(IDevice device)
        {
            if (!this._currentScanDeviceFilter(device))
                return;

            this.DeviceAdvertised(this, new DeviceEventArgs {Device = device});

            // TODO (sms): check equality implementation of device
            if (this._discoveredDevices.Contains(device))
                return;

            this._discoveredDevices.Add(device);
            this.DeviceDiscovered(this, new DeviceEventArgs {Device = device});
        }

        public void HandleConnectedDevice(IDevice device)
        {
            this.DeviceConnected(this, new DeviceEventArgs {Device = device});
        }

        public void HandleDisconnectedDevice(Boolean disconnectRequested, IDevice device)
        {
            if (disconnectRequested)
            {
                Trace.Message("DisconnectedPeripheral by user: {0}", device.Name);
                this.DeviceDisconnected(this, new DeviceEventArgs {Device = device});
            }
            else
            {
                Trace.Message("DisconnectedPeripheral by lost signal: {0}", device.Name);
                this.DeviceConnectionLost(this, new DeviceErrorEventArgs {Device = device});

                if (this.DiscoveredDevices.Contains(device)) this.DiscoveredDevices.Remove(device);
            }
        }

        public void HandleConnectionFail(IDevice device, String errorMessage)
        {
            Trace.Message("Failed to connect peripheral {0}: {1}", device.Id, device.Name);
            this.DeviceConnectionError(this, new DeviceErrorEventArgs
            {
                Device = device,
                ErrorMessage = errorMessage
            });
        }

        protected abstract Task<bool> StartScanningForDevicesNativeAsync(Guid[] serviceUuids, Boolean allowDuplicatesKey,
            CancellationToken scanCancellationToken);

        protected abstract void StopScanNative();

        protected abstract Task ConnectToDeviceNativeAsync(IDevice device, ConnectParameters connectParameters,
            CancellationToken cancellationToken);

        protected abstract void DisconnectDeviceNative(IDevice device);
    }
}