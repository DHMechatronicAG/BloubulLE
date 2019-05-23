using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using DH.BloubulLE.Contracts;
using Microsoft.Toolkit.Uwp.Connectivity;

namespace DH.BloubulLE
{
    internal class Service : ServiceBase
    {
        private readonly ObservableBluetoothLEDevice _nativeDevice;
        private readonly GattDeviceService _nativeService;

        public Service(GattDeviceService service, IDevice device) : base(device)
        {
            this._nativeDevice = (ObservableBluetoothLEDevice) device.NativeDevice;
            this._nativeService = service;
        }

        public override Guid Id => this._nativeService.Uuid;

        //method to get parent devices to check if primary is obselete
        //return true as a placeholder
        public override Boolean IsPrimary => true;

        protected override async Task<IList<ICharacteristic>> GetCharacteristicsNativeAsync()
        {
            IReadOnlyList<GattCharacteristic> nativeChars =
                (await this._nativeService.GetCharacteristicsAsync()).Characteristics;
            List<ICharacteristic> charList = new List<ICharacteristic>();
            foreach (GattCharacteristic nativeChar in nativeChars)
            {
                Characteristic characteristic = new Characteristic(nativeChar, this);
                charList.Add(characteristic);
            }

            return charList;
        }
    }
}