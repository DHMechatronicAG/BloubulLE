using System;

namespace DH.BloubulLE.Exceptions
{
    public class CharacteristicReadException : Exception
    {
        public CharacteristicReadException(String message) : base(message)
        {
        }
    }
}