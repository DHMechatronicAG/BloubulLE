using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.Bluetooth;
using DH.BloubulLE.Contracts;

namespace DH.BloubulLE
{
    public class Service : ServiceBase
    {
        private readonly BluetoothGatt _gatt;
        private readonly IGattCallback _gattCallback;
        private readonly BluetoothGattService _nativeService;

        public Service(BluetoothGattService nativeService, BluetoothGatt gatt, IGattCallback gattCallback,
            IDevice device) : base(device)
        {
            this._nativeService = nativeService;
            this._gatt = gatt;
            this._gattCallback = gattCallback;
        }

        public override Guid Id => Guid.ParseExact(this._nativeService.Uuid.ToString(), "d");
        public override Boolean IsPrimary => this._nativeService.Type == GattServiceType.Primary;

        protected override Task<IList<ICharacteristic>> GetCharacteristicsNativeAsync()
        {
            return Task.FromResult<IList<ICharacteristic>>(this._nativeService.Characteristics
                .Select(characteristic => new Characteristic(characteristic, this._gatt, this._gattCallback, this))
                .Cast<ICharacteristic>().ToList());
        }
    }
}