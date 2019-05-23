using System;
using DH.BloubulLE.EventArgs;

namespace DH.BloubulLE.Contracts
{
    /// <summary>
    /// Manages the bluetooth LE functionality of the device (usually your smartphone).
    /// </summary>
    public interface IBluetoothLE
    {
        /// <summary>
        /// State of the bluetooth LE.
        /// </summary>
        BluetoothState State { get; }

        /// <summary>
        /// Indicates whether the device can communicate via bluetooth low energy.
        /// </summary>
        Boolean IsAvailable { get; }

        /// <summary>
        /// Indicates whether the bluetooth adapter is turned on or not.
        /// <c>true</c> if <see cref="State"/> is <c>BluetoothState.On</c>
        /// </summary>
        Boolean IsOn { get; }

        /// <summary>
        /// Adapter to that provides access to the physical bluetooth adapter.
        /// </summary>
        IAdapter Adapter { get; }

        /// <summary>
        /// Occurs when <see cref="State"/> has changed.
        /// </summary>
        event EventHandler<BluetoothStateChangedArgs> StateChanged;
    }
}