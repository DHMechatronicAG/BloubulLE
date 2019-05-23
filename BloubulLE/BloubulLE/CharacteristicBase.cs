using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DH.BloubulLE.Contracts;
using DH.BloubulLE.EventArgs;

namespace DH.BloubulLE
{
    public abstract class CharacteristicBase : ICharacteristic
    {
        private IList<IDescriptor> _descriptors;
        private CharacteristicWriteType _writeType = CharacteristicWriteType.Default;

        protected CharacteristicBase(IService service)
        {
            this.Service = service;
        }

        public abstract event EventHandler<CharacteristicUpdatedEventArgs> ValueUpdated;

        public abstract Guid Id { get; }
        public abstract String Uuid { get; }
        public abstract Byte[] Value { get; }
        public String Name => KnownCharacteristics.Lookup(this.Id).Name;
        public abstract CharacteristicPropertyType Properties { get; }
        public IService Service { get; }

        public CharacteristicWriteType WriteType
        {
            get => this._writeType;
            set
            {
                if (value == CharacteristicWriteType.WithResponse &&
                    !this.Properties.HasFlag(CharacteristicPropertyType.Write) ||
                    value == CharacteristicWriteType.WithoutResponse &&
                    !this.Properties.HasFlag(CharacteristicPropertyType.WriteWithoutResponse))
                    throw new InvalidOperationException($"Write type {value} is not supported");

                this._writeType = value;
            }
        }

        public Boolean CanRead => this.Properties.HasFlag(CharacteristicPropertyType.Read);

        public Boolean CanUpdate =>
            this.Properties.HasFlag(CharacteristicPropertyType.Notify) |
            this.Properties.HasFlag(CharacteristicPropertyType.Indicate);

        public Boolean CanWrite =>
            this.Properties.HasFlag(CharacteristicPropertyType.Write) |
            this.Properties.HasFlag(CharacteristicPropertyType.WriteWithoutResponse);

        public String StringValue
        {
            get
            {
                Byte[] val = this.Value;
                if (val == null)
                    return String.Empty;

                return Encoding.UTF8.GetString(val, 0, val.Length);
            }
        }

        public async Task<Byte[]> ReadAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!this.CanRead) throw new InvalidOperationException("Characteristic does not support read.");

            Trace.Message("Characteristic.ReadAsync");
            return await this.ReadNativeAsync();
        }

        public async Task<Boolean> WriteAsync(Byte[] data,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (!this.CanWrite) throw new InvalidOperationException("Characteristic does not support write.");

            CharacteristicWriteType writeType = this.GetWriteType();

            Trace.Message("Characteristic.WriteAsync");
            return await this.WriteNativeAsync(data, writeType);
        }

        public Task StartUpdatesAsync()
        {
            if (!this.CanUpdate) throw new InvalidOperationException("Characteristic does not support update.");

            Trace.Message("Characteristic.StartUpdates");
            return this.StartUpdatesNativeAsync();
        }

        public Task StopUpdatesAsync()
        {
            if (!this.CanUpdate) throw new InvalidOperationException("Characteristic does not support update.");

            return this.StopUpdatesNativeAsync();
        }

        public async Task<IList<IDescriptor>> GetDescriptorsAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (this._descriptors == null) this._descriptors = await this.GetDescriptorsNativeAsync();
            return this._descriptors;
        }

        public async Task<IDescriptor> GetDescriptorAsync(Guid id,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            IList<IDescriptor> descriptors = await this.GetDescriptorsAsync().ConfigureAwait(false);
            return descriptors.FirstOrDefault(d => d.Id == id);
        }

        private CharacteristicWriteType GetWriteType()
        {
            if (this.WriteType != CharacteristicWriteType.Default)
                return this.WriteType;

            return this.Properties.HasFlag(CharacteristicPropertyType.Write)
                ? CharacteristicWriteType.WithResponse
                : CharacteristicWriteType.WithoutResponse;
        }

        protected abstract Task<IList<IDescriptor>> GetDescriptorsNativeAsync();
        protected abstract Task<Byte[]> ReadNativeAsync();
        protected abstract Task<Boolean> WriteNativeAsync(Byte[] data, CharacteristicWriteType writeType);
        protected abstract Task StartUpdatesNativeAsync();
        protected abstract Task StopUpdatesNativeAsync();
    }
}