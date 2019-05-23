using Android.Bluetooth;

namespace DH.BloubulLE.Extensions
{
    internal static class GattWriteTypeExtension
    {
        public static CharacteristicWriteType ToCharacteristicWriteType(this GattWriteType writeType)
        {
            if (writeType.HasFlag(GattWriteType.NoResponse)) return CharacteristicWriteType.WithoutResponse;
            return CharacteristicWriteType.WithResponse;
        }
    }
}