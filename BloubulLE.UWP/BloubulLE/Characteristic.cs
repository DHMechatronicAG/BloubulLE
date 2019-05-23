using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Security.Cryptography;
using DH.BloubulLE.Contracts;
using DH.BloubulLE.EventArgs;

namespace DH.BloubulLE
{
    public class Characteristic : CharacteristicBase
    {
        private readonly GattCharacteristic _nativeCharacteristic;

        /// <summary>
        /// Value of the characteristic to be stored locally after
        /// update notification or read
        /// </summary>
        private Byte[] _value;


        public Characteristic(GattCharacteristic nativeCharacteristic, IService service) : base(service)
        {
            this._nativeCharacteristic = nativeCharacteristic;
        }

        public override Guid Id => this._nativeCharacteristic.Uuid;
        public override String Uuid => this._nativeCharacteristic.Uuid.ToString();

        public override CharacteristicPropertyType Properties =>
            (CharacteristicPropertyType) (Int32) this._nativeCharacteristic.CharacteristicProperties;

        public override Byte[] Value
        {
            get
            {
                //return empty array if value is equal to null
                if (this._value == null) return new Byte[0];
                return this._value;
            }
        }

        public override event EventHandler<CharacteristicUpdatedEventArgs> ValueUpdated;

        protected override async Task<IList<IDescriptor>> GetDescriptorsNativeAsync()
        {
            IReadOnlyList<GattDescriptor> nativeDescriptors =
                (await this._nativeCharacteristic.GetDescriptorsAsync()).Descriptors;
            List<IDescriptor> descriptorList = new List<IDescriptor>();
            //convert to generic descriptors
            foreach (GattDescriptor nativeDescriptor in nativeDescriptors)
            {
                Descriptor descriptor = new Descriptor(nativeDescriptor, this);
                descriptorList.Add(descriptor);
            }

            return descriptorList;
        }

        protected override async Task<Byte[]> ReadNativeAsync()
        {
            Byte[] readResult = (await this._nativeCharacteristic.ReadValueAsync()).Value.ToArray();
            this._value = readResult;
            return readResult;
        }

        protected override async Task StartUpdatesNativeAsync()
        {
            this._nativeCharacteristic.ValueChanged += this.OnCharacteristicValueChanged;
            GattWriteResult result =
                await this._nativeCharacteristic.WriteClientCharacteristicConfigurationDescriptorWithResultAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify);
            //output trace message with status of update
            if (result.Status == GattCommunicationStatus.Success)
                Trace.Message("Start Updates Successful");
            else if (result.Status == GattCommunicationStatus.AccessDenied)
                Trace.Message("Incorrect permissions to start updates");
            else if (result.Status == GattCommunicationStatus.ProtocolError && result.ProtocolError != null)
                Trace.Message("Start updates returned with error: {0}", this.parseError(result.ProtocolError));
            else if (result.Status == GattCommunicationStatus.ProtocolError)
                Trace.Message("Start updates returned with unknown error");
            else if (result.Status == GattCommunicationStatus.Unreachable)
                Trace.Message("Characteristic properties are unreachable");
        }

        protected override async Task StopUpdatesNativeAsync()
        {
            this._nativeCharacteristic.ValueChanged -= this.OnCharacteristicValueChanged;
            GattWriteResult result =
                await this._nativeCharacteristic.WriteClientCharacteristicConfigurationDescriptorWithResultAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.None);
            if (result.Status == GattCommunicationStatus.Success)
                Trace.Message("Stop Updates Successful");
            else if (result.Status == GattCommunicationStatus.AccessDenied)
                Trace.Message("Incorrect permissions to stop updates");
            else if (result.Status == GattCommunicationStatus.ProtocolError && result.ProtocolError != null)
                Trace.Message("Stop updates returned with error: {0}", this.parseError(result.ProtocolError));
            else if (result.Status == GattCommunicationStatus.ProtocolError)
                Trace.Message("Stop updates returned with unknown error");
            else if (result.Status == GattCommunicationStatus.Unreachable)
                Trace.Message("Characteristic properties are unreachable");
        }

        protected override async Task<Boolean> WriteNativeAsync(Byte[] data, CharacteristicWriteType writeType)
        {
            //print errors if error and write with response
            if (writeType == CharacteristicWriteType.WithResponse)
            {
                GattWriteResult result =
                    await this._nativeCharacteristic.WriteValueWithResultAsync(
                        CryptographicBuffer.CreateFromByteArray(data));
                if (result.Status == GattCommunicationStatus.Success)
                {
                    Trace.Message("Write successful");
                    return true;
                }

                if (result.Status == GattCommunicationStatus.AccessDenied)
                    Trace.Message("Incorrect permissions to stop updates");
                else if (result.Status == GattCommunicationStatus.ProtocolError && result.ProtocolError != null)
                    Trace.Message("Write Characteristic returned with error: {0}",
                        this.parseError(result.ProtocolError));
                else if (result.Status == GattCommunicationStatus.ProtocolError)
                    Trace.Message("Write Characteristic returned with unknown error");
                else if (result.Status == GattCommunicationStatus.Unreachable)
                    Trace.Message("Characteristic write is unreachable");
                return false;
            }

            GattCommunicationStatus status =
                await this._nativeCharacteristic.WriteValueAsync(CryptographicBuffer.CreateFromByteArray(data),
                    GattWriteOption.WriteWithoutResponse);
            if (status == GattCommunicationStatus.Success) return true;
            return false;
        }

        /// <summary>
        /// Handler for when the characteristic value is changed. Updates the
        /// stored value
        /// </summary>
        private void OnCharacteristicValueChanged(Object sender, GattValueChangedEventArgs e)
        {
            this._value = e.CharacteristicValue.ToArray(); //add value to array
            this.ValueUpdated?.Invoke(this, new CharacteristicUpdatedEventArgs(this));
        }

        /// <summary>
        /// Used to parse errors returned by UWP methods in byte form
        /// </summary>
        /// <param name="err">The byte describing the type of error</param>
        /// <returns>Returns a string with the name of an error byte</returns>
        private String parseError(Byte? err)
        {
            if (err == GattProtocolError.AttributeNotFound) return "Attribute Not Found";
            if (err == GattProtocolError.AttributeNotLong) return "Attribute Not Long";
            if (err == GattProtocolError.InsufficientAuthentication) return "Insufficient Authentication";
            if (err == GattProtocolError.InsufficientAuthorization) return "Insufficient Authorization";
            if (err == GattProtocolError.InsufficientEncryption) return "Insufficient Encryption";
            if (err == GattProtocolError.InsufficientEncryptionKeySize) return "Insufficient Encryption Key Size";
            if (err == GattProtocolError.InsufficientResources) return "Insufficient Resource";
            if (err == GattProtocolError.InvalidAttributeValueLength) return "Invalid Attribute Value Length";
            if (err == GattProtocolError.InvalidHandle) return "Invalid Handle";
            if (err == GattProtocolError.InvalidOffset) return "Invalid Offset";
            if (err == GattProtocolError.InvalidPdu) return "Invalid PDU";
            if (err == GattProtocolError.PrepareQueueFull) return "Prepare Queue Full";
            if (err == GattProtocolError.ReadNotPermitted) return "Read Not Permitted";
            if (err == GattProtocolError.RequestNotSupported) return "Request Not Supported";
            if (err == GattProtocolError.UnlikelyError) return "Unlikely Error";
            if (err == GattProtocolError.UnsupportedGroupType) return "Unsupported Group Type";
            if (err == GattProtocolError.WriteNotPermitted) return "Write Not Permitted";
            return null;
        }
    }
}