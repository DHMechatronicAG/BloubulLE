using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DH.BloubulLE.Contracts;

namespace DH.BloubulLE
{
    public abstract class ServiceBase : IService
    {
        private readonly List<ICharacteristic> _characteristics = new List<ICharacteristic>();

        protected ServiceBase(IDevice device)
        {
            this.Device = device;
        }

        public String Name => KnownServices.Lookup(this.Id).Name;
        public abstract Guid Id { get; }
        public abstract Boolean IsPrimary { get; }
        public IDevice Device { get; }

        public async Task<IList<ICharacteristic>> GetCharacteristicsAsync()
        {
            if (!this._characteristics.Any())
                this._characteristics.AddRange(await this.GetCharacteristicsNativeAsync());

            // make a copy here so that the caller cant modify the original list
            return this._characteristics.ToList();
        }

        public async Task<ICharacteristic> GetCharacteristicAsync(Guid id)
        {
            IList<ICharacteristic> characteristics = await this.GetCharacteristicsAsync();
            return characteristics.FirstOrDefault(c => c.Id == id);
        }

        public virtual void Dispose()
        {
        }

        protected abstract Task<IList<ICharacteristic>> GetCharacteristicsNativeAsync();
    }
}