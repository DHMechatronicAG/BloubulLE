using System;

namespace DH.BloubulLE.Exceptions
{
    public class DeviceDiscoverException : Exception
    {
        public DeviceDiscoverException() : base("Could not find the specific device.")
        {
        }
    }
}