using System;
using CoreBluetooth;

namespace DH.BloubulLE.Extensions
{
    internal static class CharacteristicWriteTypeExtension
    {
        public static CBCharacteristicWriteType ToNative(this CharacteristicWriteType writeType)
        {
            switch (writeType)
            {
                case CharacteristicWriteType.WithResponse:
                    return CBCharacteristicWriteType.WithResponse;
                case CharacteristicWriteType.WithoutResponse:
                    return CBCharacteristicWriteType.WithoutResponse;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}