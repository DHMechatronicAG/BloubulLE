using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DH.BloubulLE.Contracts
{
    /// <summary>
    /// A bluetooth LE GATT service.
    /// </summary>
    public interface IService : IDisposable
    {
        /// <summary>
        /// Id of the Service.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Name of the service.
        /// Returns the name if the <see cref="Id"/> is a standard Id. See <see cref="KnownServices"/>.
        /// </summary>
        String Name { get; }

        /// <summary>
        /// Indicates whether the type of service is primary or secondary.
        /// </summary>
        Boolean IsPrimary { get; }

        /// <summary>
        /// Returns the parent device.
        /// </summary>
        IDevice Device { get; }

        /// <summary>
        /// Gets the characteristics of the service.
        /// </summary>
        /// <returns>A task that represents the asynchronous read operation. The Result property will contain a list of characteristics.</returns>
        Task<IList<ICharacteristic>> GetCharacteristicsAsync();

        /// <summary>
        /// Gets the first characteristic with the Id <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The id of the searched characteristic.</param>
        /// <returns>
        /// A task that represents the asynchronous read operation.
        /// The Result property will contain the characteristic with the specified <paramref name="id"/>.
        /// If the characteristic doesn't exist, the Result will be null.
        /// </returns>
        Task<ICharacteristic> GetCharacteristicAsync(Guid id);
    }
}