using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.Bluetooth;
using DH.BloubulLE.CallbackEventArgs;
using DH.BloubulLE.Contracts;
using DH.BloubulLE.EventArgs;
using DH.BloubulLE.Exceptions;
using DH.BloubulLE.Extensions;
using DH.BloubulLE.Utils;

namespace DH.BloubulLE
{
    public class Characteristic : CharacteristicBase
    {
        //https://developer.android.com/samples/BluetoothLeGatt/src/com.example.android.bluetoothlegatt/SampleGattAttributes.html

        private static readonly Guid ClientCharacteristicConfigurationDescriptorId =
            Guid.Parse("00002902-0000-1000-8000-00805f9b34fb");

        private readonly BluetoothGatt _gatt;
        private readonly IGattCallback _gattCallback;
        private readonly BluetoothGattCharacteristic _nativeCharacteristic;

        public Characteristic(BluetoothGattCharacteristic nativeCharacteristic, BluetoothGatt gatt,
            IGattCallback gattCallback, IService service) : base(service)
        {
            this._nativeCharacteristic = nativeCharacteristic;
            this._gatt = gatt;
            this._gattCallback = gattCallback;
        }

        public override Guid Id => Guid.Parse(this._nativeCharacteristic.Uuid.ToString());
        public override String Uuid => this._nativeCharacteristic.Uuid.ToString();
        public override Byte[] Value => this._nativeCharacteristic.GetValue() ?? new Byte[0];

        public override CharacteristicPropertyType Properties =>
            (CharacteristicPropertyType) (Int32) this._nativeCharacteristic.Properties;

        public override event EventHandler<CharacteristicUpdatedEventArgs> ValueUpdated;

        protected override Task<IList<IDescriptor>> GetDescriptorsNativeAsync()
        {
            return Task.FromResult<IList<IDescriptor>>(this._nativeCharacteristic.Descriptors
                .Select(item => new Descriptor(item, this._gatt, this._gattCallback, this)).Cast<IDescriptor>()
                .ToList());
        }

        protected override async Task<Byte[]> ReadNativeAsync()
        {
            return await TaskBuilder.FromEvent<Byte[], EventHandler<CharacteristicReadCallbackEventArgs>, EventHandler>(
                this.ReadInternal,
                (complete, reject) => (sender, args) =>
                {
                    if (args.Characteristic.Uuid == this._nativeCharacteristic.Uuid)
                        complete(args.Characteristic.GetValue());
                },
                handler => this._gattCallback.CharacteristicValueUpdated += handler,
                handler => this._gattCallback.CharacteristicValueUpdated -= handler,
                reject => (sender, args) =>
                {
                    reject(new Exception(
                        $"Device '{this.Service.Device.Id}' disconnected while reading characteristic with {this.Id}."));
                },
                handler => this._gattCallback.ConnectionInterrupted += handler,
                handler => this._gattCallback.ConnectionInterrupted -= handler);
        }

        private void ReadInternal()
        {
            if (!this._gatt.ReadCharacteristic(this._nativeCharacteristic))
                throw new CharacteristicReadException("BluetoothGattCharacteristic.readCharacteristic returned FALSE");
        }

        protected override async Task<Boolean> WriteNativeAsync(Byte[] data, CharacteristicWriteType writeType)
        {
            this._nativeCharacteristic.WriteType = writeType.ToNative();

            return await TaskBuilder
                .FromEvent<Boolean, EventHandler<CharacteristicWriteCallbackEventArgs>, EventHandler>(
                    () => this.InternalWrite(data),
                    (complete, reject) => (sender, args) =>
                    {
                        if (args.Characteristic.Uuid == this._nativeCharacteristic.Uuid)
                            complete(args.Exception == null);
                    },
                    handler => this._gattCallback.CharacteristicValueWritten += handler,
                    handler => this._gattCallback.CharacteristicValueWritten -= handler,
                    reject => (sender, args) =>
                    {
                        reject(new Exception(
                            $"Device '{this.Service.Device.Id}' disconnected while writing characteristic with {this.Id}."));
                    },
                    handler => this._gattCallback.ConnectionInterrupted += handler,
                    handler => this._gattCallback.ConnectionInterrupted -= handler);
        }

        private void InternalWrite(Byte[] data)
        {
            if (!this._nativeCharacteristic.SetValue(data))
                throw new CharacteristicReadException("Gatt characteristic set value FAILED.");

            Trace.Message("Write {0}", this.Id);

            if (!this._gatt.WriteCharacteristic(this._nativeCharacteristic))
                throw new CharacteristicReadException("Gatt write characteristic FAILED.");
        }

        protected override async Task StartUpdatesNativeAsync()
        {
            // wire up the characteristic value updating on the gattcallback for event forwarding
            this._gattCallback.CharacteristicValueUpdated += this.OnCharacteristicValueChanged;

            if (!this._gatt.SetCharacteristicNotification(this._nativeCharacteristic, true))
                throw new CharacteristicReadException("Gatt SetCharacteristicNotification FAILED.");

            // In order to subscribe to notifications on a given characteristic, you must first set the Notifications Enabled bit
            // in its Client Characteristic Configuration Descriptor. See https://developer.bluetooth.org/gatt/descriptors/Pages/DescriptorsHomePage.aspx and
            // https://developer.bluetooth.org/gatt/descriptors/Pages/DescriptorViewer.aspx?u=org.bluetooth.descriptor.gatt.client_characteristic_configuration.xml
            // for details.

            await Task.Delay(100);
            // this might be because we need to wait on SetCharacteristicNotification ...maybe there alos is a callback for this?
            //ToDo is this still needed?

            if (this._nativeCharacteristic.Descriptors.Count > 0)
            {
                IList<IDescriptor> descriptors = await this.GetDescriptorsAsync();
                IDescriptor descriptor =
                    descriptors.FirstOrDefault(d => d.Id.Equals(ClientCharacteristicConfigurationDescriptorId)) ??
                    descriptors.FirstOrDefault(); // fallback just in case manufacturer forgot

                //has to have one of these (either indicate or notify)
                if (this.Properties.HasFlag(CharacteristicPropertyType.Indicate))
                {
                    await descriptor.WriteAsync(BluetoothGattDescriptor.EnableIndicationValue.ToArray());
                    Trace.Message("Descriptor set value: INDICATE");
                }

                if (this.Properties.HasFlag(CharacteristicPropertyType.Notify))
                {
                    await descriptor.WriteAsync(BluetoothGattDescriptor.EnableNotificationValue.ToArray());
                    Trace.Message("Descriptor set value: NOTIFY");
                }
            }
            else
            {
                Trace.Message("Descriptor set value FAILED: _nativeCharacteristic.Descriptors was empty");
            }

            Trace.Message("Characteristic.StartUpdates, successful!");
        }

        protected override async Task StopUpdatesNativeAsync()
        {
            this._gattCallback.CharacteristicValueUpdated -= this.OnCharacteristicValueChanged;

            Boolean successful = this._gatt.SetCharacteristicNotification(this._nativeCharacteristic, false);

            Trace.Message("Characteristic.StopUpdatesNative, successful: {0}", successful);

            if (!successful)
                throw new CharacteristicReadException("GATT: SetCharacteristicNotification to false, FAILED.");

            if (this._nativeCharacteristic.Descriptors.Count > 0)
            {
                IList<IDescriptor> descriptors = await this.GetDescriptorsAsync();
                IDescriptor descriptor =
                    descriptors.FirstOrDefault(d => d.Id.Equals(ClientCharacteristicConfigurationDescriptorId)) ??
                    descriptors.FirstOrDefault(); // fallback just in case manufacturer forgot

                if (this.Properties.HasFlag(CharacteristicPropertyType.Notify) ||
                    this.Properties.HasFlag(CharacteristicPropertyType.Indicate))
                {
                    await descriptor.WriteAsync(BluetoothGattDescriptor.DisableNotificationValue.ToArray());
                    Trace.Message("Descriptor set value: DISABLE_NOTIFY");
                }
            }
            else
            {
                Trace.Message("Descriptor set value FAILED: _nativeCharacteristic.Descriptors was empty");
            }
        }

        private void OnCharacteristicValueChanged(Object sender, CharacteristicReadCallbackEventArgs e)
        {
            if (e.Characteristic.Uuid == this._nativeCharacteristic.Uuid)
                this.ValueUpdated?.Invoke(this, new CharacteristicUpdatedEventArgs(this));
        }
    }
}