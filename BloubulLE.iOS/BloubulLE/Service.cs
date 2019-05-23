using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoreBluetooth;
using DH.BloubulLE.Contracts;
using DH.BloubulLE.Extensions;
using DH.BloubulLE.Utils;

namespace DH.BloubulLE
{
    public class Service : ServiceBase
    {
        private readonly CBCentralManager _centralManager;
        private readonly CBPeripheral _device;
        private readonly CBService _service;

        public Service(CBService service, IDevice device, CBCentralManager centralManager)
            : base(device)
        {
            this._service = service;
            this._device = device.NativeDevice as CBPeripheral;
            this._centralManager = centralManager;
        }

        public override Guid Id => this._service.UUID.GuidFromUuid();
        public override Boolean IsPrimary => this._service.Primary;

        protected override Task<IList<ICharacteristic>> GetCharacteristicsNativeAsync()
        {
            Exception exception =
                new Exception(
                    $"Device '{this.Device.Id}' disconnected while fetching characteristics for service with {this.Id}.");

            return TaskBuilder
                .FromEvent<IList<ICharacteristic>, EventHandler<CBServiceEventArgs>,
                    EventHandler<CBPeripheralErrorEventArgs>>(
                    () =>
                    {
                        if (this._device.State != CBPeripheralState.Connected)
                            throw exception;

                        this._device.DiscoverCharacteristics(this._service);
                    },
                    (complete, reject) => (sender, args) =>
                    {
                        if (args.Error != null)
                        {
                            reject(new Exception($"Discover characteristics error: {args.Error.Description}"));
                        }
                        else if (args.Service?.Characteristics == null)
                        {
                            reject(new Exception("Discover characteristics error: returned list is null"));
                        }
                        else
                        {
                            List<ICharacteristic> characteristics = args.Service.Characteristics
                                .Select(characteristic =>
                                    new Characteristic(characteristic, this._device, this, this._centralManager))
                                .Cast<ICharacteristic>().ToList();
                            complete(characteristics);
                        }
                    },
                    handler => this._device.DiscoveredCharacteristic += handler,
                    handler => this._device.DiscoveredCharacteristic -= handler,
                    reject => (sender, args) =>
                    {
                        if (args.Peripheral.Identifier == this._device.Identifier)
                            reject(exception);
                    },
                    handler => this._centralManager.DisconnectedPeripheral += handler,
                    handler => this._centralManager.DisconnectedPeripheral -= handler);
        }
    }
}