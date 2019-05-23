using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Security.Cryptography;
using DH.BloubulLE.Contracts;

namespace DH.BloubulLE
{
    public class Descriptor : DescriptorBase
    {
        private readonly GattDescriptor _nativeDescriptor;

        /// <summary>
        /// The locally stored value of a descriptor updated after a
        /// notification or a read
        /// </summary>
        private Byte[] _value;

        public Descriptor(GattDescriptor nativeDescriptor, ICharacteristic characteristic) : base(characteristic)
        {
            this._nativeDescriptor = nativeDescriptor;
        }

        public override Guid Id => this._nativeDescriptor.Uuid;

        public override Byte[] Value
        {
            get
            {
                if (this._value == null) return new Byte[0];
                return this._value;
            }
        }

        protected override async Task<Byte[]> ReadNativeAsync()
        {
            GattReadResult readResult = await this._nativeDescriptor.ReadValueAsync();
            if (readResult.Status == GattCommunicationStatus.Success)
                Trace.Message("Descriptor Read Successfully");
            else
                Trace.Message("Descriptor Read Failed");
            this._value = readResult.Value.ToArray();
            return this._value;
        }

        protected override async Task WriteNativeAsync(Byte[] data)
        {
            //method contains no option for writing with response, so always write
            //without response
            GattCommunicationStatus writeResult =
                await this._nativeDescriptor.WriteValueAsync(CryptographicBuffer.CreateFromByteArray(data));
            if (writeResult == GattCommunicationStatus.Success)
                Trace.Message("Descriptor Write Successfully");
            else
                Trace.Message("Descriptor Write Failed");
        }
    }
}