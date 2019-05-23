using System;
using System.Threading;
using System.Threading.Tasks;
using DH.BloubulLE.Contracts;

namespace DH.BloubulLE
{
    public abstract class DescriptorBase : IDescriptor
    {
        private String _name;

        protected DescriptorBase(ICharacteristic characteristic)
        {
            this.Characteristic = characteristic;
        }

        public abstract Guid Id { get; }

        public String Name => this._name ?? (this._name = KnownDescriptors.Lookup(this.Id).Name);

        public abstract Byte[] Value { get; }

        public ICharacteristic Characteristic { get; }

        public Task<Byte[]> ReadAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ReadNativeAsync();
        }

        public Task WriteAsync(Byte[] data, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            return this.WriteNativeAsync(data);
        }

        protected abstract Task<Byte[]> ReadNativeAsync();

        protected abstract Task WriteNativeAsync(Byte[] data);
    }
}