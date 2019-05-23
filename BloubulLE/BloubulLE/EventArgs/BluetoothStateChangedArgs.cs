using DH.BloubulLE.Contracts;

namespace DH.BloubulLE.EventArgs
{
    public class BluetoothStateChangedArgs : System.EventArgs
    {
        public BluetoothStateChangedArgs(BluetoothState oldState, BluetoothState newState)
        {
            this.OldState = oldState;
            this.NewState = newState;
        }

        /// <summary>
        /// State before the change.
        /// </summary>
        public BluetoothState OldState { get; }

        /// <summary>
        /// Current state.
        /// </summary>
        public BluetoothState NewState { get; }
    }
}