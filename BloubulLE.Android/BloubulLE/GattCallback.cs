using System;
using Android.Bluetooth;
using DH.BloubulLE.CallbackEventArgs;
using DH.BloubulLE.Extensions;

namespace DH.BloubulLE
{
    public interface IGattCallback
    {
        event EventHandler<ServicesDiscoveredCallbackEventArgs> ServicesDiscovered;
        event EventHandler<CharacteristicReadCallbackEventArgs> CharacteristicValueUpdated;
        event EventHandler<CharacteristicWriteCallbackEventArgs> CharacteristicValueWritten;
        event EventHandler<DescriptorCallbackEventArgs> DescriptorValueWritten;
        event EventHandler<DescriptorCallbackEventArgs> DescriptorValueRead;
        event EventHandler<RssiReadCallbackEventArgs> RemoteRssiRead;
        event EventHandler ConnectionInterrupted;
        event EventHandler<MtuRequestCallbackEventArgs> MtuRequested;
    }

    public class GattCallback : BluetoothGattCallback, IGattCallback
    {
        private readonly Adapter _adapter;
        private readonly Device _device;

        public GattCallback(Adapter adapter, Device device)
        {
            this._adapter = adapter;
            this._device = device;
        }

        public event EventHandler<ServicesDiscoveredCallbackEventArgs> ServicesDiscovered;
        public event EventHandler<CharacteristicReadCallbackEventArgs> CharacteristicValueUpdated;
        public event EventHandler<CharacteristicWriteCallbackEventArgs> CharacteristicValueWritten;
        public event EventHandler<RssiReadCallbackEventArgs> RemoteRssiRead;
        public event EventHandler ConnectionInterrupted;
        public event EventHandler<DescriptorCallbackEventArgs> DescriptorValueWritten;
        public event EventHandler<DescriptorCallbackEventArgs> DescriptorValueRead;
        public event EventHandler<MtuRequestCallbackEventArgs> MtuRequested;

        public override void OnConnectionStateChange(BluetoothGatt gatt, GattStatus status, ProfileState newState)
        {
            base.OnConnectionStateChange(gatt, status, newState);

            if (!gatt.Device.Address.Equals(this._device.BluetoothDevice.Address))
            {
                Trace.Message(
                    $"Gatt callback for device {this._device.BluetoothDevice.Address} was called for device with address {gatt.Device.Address}. This shoud not happen. Please log an issue.");
                return;
            }

            //ToDo ignore just for me
            Trace.Message(
                $"References of parent device and gatt callback device equal? {ReferenceEquals(this._device.BluetoothDevice, gatt.Device).ToString().ToUpper()}");

            Trace.Message($"OnConnectionStateChange: GattStatus: {status}");

            switch (newState)
            {
                // disconnected
                case ProfileState.Disconnected:

                    // Close GATT regardless, else we can accumulate zombie gatts.
                    this.CloseGattInstances(gatt);

                    // If status == 19, then connection was closed by the peripheral device (clean disconnect), consider this as a DeviceDisconnected
                    if (this._device.IsOperationRequested || (Int32) status == 19)
                    {
                        Trace.Message("Disconnected by user");

                        //Found so we can remove it
                        this._device.IsOperationRequested = false;
                        this._adapter.ConnectedDeviceRegistry.Remove(gatt.Device.Address);

                        if (status != GattStatus.Success && (Int32) status != 19)
                        {
                            // The above error event handles the case where the error happened during a Connect call, which will close out any waiting asyncs.
                            // Android > 5.0 uses this switch branch when an error occurs during connect
                            Trace.Message(
                                $"Error while connecting '{this._device.Name}'. Not raising disconnect event.");
                            this._adapter.HandleConnectionFail(this._device, $"GattCallback error: {status}");
                        }
                        else
                        {
                            //we already hadled device error so no need th raise disconnect event(happens when device not in range)
                            this._adapter.HandleDisconnectedDevice(true, this._device);
                        }

                        break;
                    }

                    //connection must have been lost, because the callback was not triggered by calling disconnect
                    Trace.Message($"Disconnected '{this._device.Name}' by lost connection");

                    this._adapter.ConnectedDeviceRegistry.Remove(gatt.Device.Address);
                    this._adapter.HandleDisconnectedDevice(false, this._device);

                    // inform pending tasks
                    this.ConnectionInterrupted?.Invoke(this, System.EventArgs.Empty);
                    break;
                // connecting
                case ProfileState.Connecting:
                    Trace.Message("Connecting");
                    break;
                // connected
                case ProfileState.Connected:
                    Trace.Message("Connected");

                    //Check if the operation was requested by the user                    
                    if (this._device.IsOperationRequested)
                    {
                        this._device.Update(gatt.Device, gatt);

                        //Found so we can remove it
                        this._device.IsOperationRequested = false;
                    }
                    else
                    {
                        //ToDo explore this
                        //only for on auto-reconnect (device is not in operation registry)
                        this._device.Update(gatt.Device, gatt);
                    }

                    if (status != GattStatus.Success)
                    {
                        // The above error event handles the case where the error happened during a Connect call, which will close out any waiting asyncs.
                        // Android <= 4.4 uses this switch branch when an error occurs during connect
                        Trace.Message($"Error while connecting '{this._device.Name}'. GattStatus: {status}. ");
                        this._adapter.HandleConnectionFail(this._device, $"GattCallback error: {status}");

                        this.CloseGattInstances(gatt);
                    }
                    else
                    {
                        this._adapter.ConnectedDeviceRegistry[gatt.Device.Address] = this._device;
                        this._adapter.HandleConnectedDevice(this._device);
                    }

                    break;
                // disconnecting
                case ProfileState.Disconnecting:
                    Trace.Message("Disconnecting");
                    break;
            }
        }

        private void CloseGattInstances(BluetoothGatt gatt)
        {
            //ToDO just for me
            Trace.Message(
                $"References of parnet device gatt and callback gatt equal? {ReferenceEquals(this._device._gatt, gatt).ToString().ToUpper()}");

            if (!ReferenceEquals(gatt, this._device._gatt)) gatt.Close();

            //cleanup everything else
            this._device.CloseGatt();
        }

        public override void OnServicesDiscovered(BluetoothGatt gatt, GattStatus status)
        {
            base.OnServicesDiscovered(gatt, status);

            Trace.Message("OnServicesDiscovered: {0}", status.ToString());

            this.ServicesDiscovered?.Invoke(this, new ServicesDiscoveredCallbackEventArgs());
        }

        public override void OnCharacteristicRead(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic,
            GattStatus status)
        {
            base.OnCharacteristicRead(gatt, characteristic, status);

            Trace.Message("OnCharacteristicRead: value {0}; status {1}", characteristic.GetValue().ToHexString(),
                status);

            this.CharacteristicValueUpdated?.Invoke(this, new CharacteristicReadCallbackEventArgs(characteristic));
        }

        public override void OnCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic)
        {
            base.OnCharacteristicChanged(gatt, characteristic);

            this.CharacteristicValueUpdated?.Invoke(this, new CharacteristicReadCallbackEventArgs(characteristic));
        }

        public override void OnCharacteristicWrite(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic,
            GattStatus status)
        {
            base.OnCharacteristicWrite(gatt, characteristic, status);

            Trace.Message("OnCharacteristicWrite: value {0} status {1}", characteristic.GetValue().ToHexString(),
                status);

            this.CharacteristicValueWritten?.Invoke(this,
                new CharacteristicWriteCallbackEventArgs(characteristic, this.GetExceptionFromGattStatus(status)));
        }

        public override void OnReliableWriteCompleted(BluetoothGatt gatt, GattStatus status)
        {
            base.OnReliableWriteCompleted(gatt, status);

            Trace.Message("OnReliableWriteCompleted: {0}", status);
        }

        public override void OnMtuChanged(BluetoothGatt gatt, Int32 mtu, GattStatus status)
        {
            base.OnMtuChanged(gatt, mtu, status);

            Trace.Message("OnMtuChanged to value: {0}", mtu);

            this.MtuRequested?.Invoke(this,
                new MtuRequestCallbackEventArgs(this.GetExceptionFromGattStatus(status), mtu));
        }

        public override void OnReadRemoteRssi(BluetoothGatt gatt, Int32 rssi, GattStatus status)
        {
            base.OnReadRemoteRssi(gatt, rssi, status);

            Trace.Message("OnReadRemoteRssi: device {0} status {1} value {2}", gatt.Device.Name, status, rssi);

            this.RemoteRssiRead?.Invoke(this,
                new RssiReadCallbackEventArgs(this.GetExceptionFromGattStatus(status), rssi));
        }

        public override void OnDescriptorWrite(BluetoothGatt gatt, BluetoothGattDescriptor descriptor,
            GattStatus status)
        {
            base.OnDescriptorWrite(gatt, descriptor, status);

            Trace.Message("OnDescriptorWrite: {0}", descriptor.GetValue()?.ToHexString());

            this.DescriptorValueWritten?.Invoke(this,
                new DescriptorCallbackEventArgs(descriptor, this.GetExceptionFromGattStatus(status)));
        }

        public override void OnDescriptorRead(BluetoothGatt gatt, BluetoothGattDescriptor descriptor, GattStatus status)
        {
            base.OnDescriptorRead(gatt, descriptor, status);

            Trace.Message("OnDescriptorRead: {0}", descriptor.GetValue()?.ToHexString());

            this.DescriptorValueRead?.Invoke(this,
                new DescriptorCallbackEventArgs(descriptor, this.GetExceptionFromGattStatus(status)));
        }

        private Exception GetExceptionFromGattStatus(GattStatus status)
        {
            Exception exception = null;
            switch (status)
            {
                case GattStatus.Failure:
                case GattStatus.InsufficientAuthentication:
                case GattStatus.InsufficientEncryption:
                case GattStatus.InvalidAttributeLength:
                case GattStatus.InvalidOffset:
                case GattStatus.ReadNotPermitted:
                case GattStatus.RequestNotSupported:
                case GattStatus.WriteNotPermitted:
                    exception = new Exception(status.ToString());
                    break;
                case GattStatus.Success:
                    break;
            }

            return exception;
        }
    }
}