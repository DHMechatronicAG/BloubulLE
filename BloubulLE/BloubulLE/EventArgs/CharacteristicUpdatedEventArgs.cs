using DH.BloubulLE.Contracts;

namespace DH.BloubulLE.EventArgs
{
    public class CharacteristicUpdatedEventArgs : System.EventArgs
    {
        public CharacteristicUpdatedEventArgs(ICharacteristic characteristic)
        {
            this.Characteristic = characteristic;
        }

        public ICharacteristic Characteristic { get; set; }
    }
}