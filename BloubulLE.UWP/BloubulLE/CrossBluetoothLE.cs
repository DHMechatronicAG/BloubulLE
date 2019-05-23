using System;
using System.Threading;
using DH.BloubulLE.Contracts;

namespace DH.BloubulLE
{
    /// <summary>
    /// Cross platform bluetooth LE implemenation.
    /// </summary>
    public static class CrossBluetoothLE
    {
        private static readonly Lazy<IBluetoothLE> Implementation =
            new Lazy<IBluetoothLE>(CreateImplementation, LazyThreadSafetyMode.PublicationOnly);

        /// <summary>
        /// Current bluetooth LE implementation.
        /// </summary>
        public static IBluetoothLE Current
        {
            get
            {
                IBluetoothLE ret = Implementation.Value;
                if (ret == null) throw NotImplementedInReferenceAssembly();
                return ret;
            }
        }

        private static IBluetoothLE CreateImplementation()
        {
#if PORTABLE
            return null;
#else
            BleImplementation implementation = new BleImplementation();
            implementation.Initialize();
            return implementation;
#endif
        }

        internal static Exception NotImplementedInReferenceAssembly()
        {
            return new NotImplementedException(
                "This functionality is not implemented in the portable version of this assembly.  You should reference the NuGet package from your main application project in order to reference the platform-specific implementation.");
        }
    }
}