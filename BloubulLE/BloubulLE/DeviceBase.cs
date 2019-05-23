using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DH.BloubulLE.Contracts;

namespace DH.BloubulLE
{
    public interface ICancellationMaster
    {
        CancellationTokenSource TokenSource { get; set; }
    }

    public static class ICancellationMasterExtensions
    {
        public static CancellationTokenSource GetCombinedSource(this ICancellationMaster cancellationMaster,
            CancellationToken token)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(cancellationMaster.TokenSource.Token, token);
        }

        public static void CancelEverything(this ICancellationMaster cancellationMaster)
        {
            cancellationMaster.TokenSource?.Cancel();
            cancellationMaster.TokenSource?.Dispose();
            cancellationMaster.TokenSource = null;
        }

        public static void CancelEverythingAndReInitialize(this ICancellationMaster cancellationMaster)
        {
            cancellationMaster.CancelEverything();
            cancellationMaster.TokenSource = new CancellationTokenSource();
        }
    }

    public abstract class DeviceBase : IDevice, ICancellationMaster
    {
        protected readonly IAdapter Adapter;
        protected readonly List<IService> KnownServices = new List<IService>();

        protected DeviceBase(IAdapter adapter)
        {
            this.Adapter = adapter;
        }

        CancellationTokenSource ICancellationMaster.TokenSource { get; set; } = new CancellationTokenSource();
        public Guid Id { get; protected set; }
        public String Name { get; protected set; }
        public Int32 Rssi { get; protected set; }
        public DeviceState State => this.GetState();
        public IList<AdvertisementRecord> AdvertisementRecords { get; protected set; }
        public abstract Object NativeDevice { get; }

        public async Task<IList<IService>> GetServicesAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!this.KnownServices.Any())
                using (CancellationTokenSource source = this.GetCombinedSource(cancellationToken))
                {
                    this.KnownServices.AddRange(await this.GetServicesNativeAsync());
                }

            return this.KnownServices;
        }

        public async Task<IService> GetServiceAsync(Guid id,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            IList<IService> services = await this.GetServicesAsync(cancellationToken);
            return services.FirstOrDefault(x => x.Id == id);
        }

        public async Task<Int32> RequestMtuAsync(Int32 requestValue)
        {
            return await this.RequestMtuNativeAsync(requestValue);
        }

        public Boolean UpdateConnectionInterval(ConnectionInterval interval)
        {
            return this.UpdateConnectionIntervalNative(interval);
        }

        public abstract Task<Boolean> UpdateRssiAsync();

        public void Dispose()
        {
            this.Adapter.DisconnectDeviceAsync(this);
        }

        protected abstract DeviceState GetState();
        protected abstract Task<IEnumerable<IService>> GetServicesNativeAsync();
        protected abstract Task<Int32> RequestMtuNativeAsync(Int32 requestValue);
        protected abstract Boolean UpdateConnectionIntervalNative(ConnectionInterval interval);

        public override String ToString()
        {
            return this.Name;
        }

        public void ClearServices()
        {
            this.CancelEverythingAndReInitialize();

            foreach (IService service in this.KnownServices)
                try
                {
                    service.Dispose();
                }
                catch (Exception ex)
                {
                    Trace.Message("Exception while cleanup of service: {0}", ex.Message);
                }

            this.KnownServices.Clear();
        }

        public override Boolean Equals(Object other)
        {
            if (other == null) return false;

            if (other.GetType() != this.GetType()) return false;

            DeviceBase otherDeviceBase = (DeviceBase) other;
            return this.Id == otherDeviceBase.Id;
        }

        public override Int32 GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }
}