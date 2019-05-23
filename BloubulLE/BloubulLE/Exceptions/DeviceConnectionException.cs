using System;

namespace DH.BloubulLE.Exceptions
{
    public class DeviceConnectionException : Exception
    {
        // TODO: maybe pass IDevice instead (after Connect refactoring)
        public DeviceConnectionException(Guid deviceId, String deviceName, String message) : base(message)
        {
            this.DeviceId = deviceId;
            this.DeviceName = deviceName;
        }

        public Guid DeviceId { get; }
        public String DeviceName { get; }
    }
}