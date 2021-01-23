using System;

namespace Common.Packets
{
    /// <summary>
    /// A class for passing packets between event handlers.
    /// </summary>
    public class PacketEventArgs : EventArgs
    {
        public Packet Packet { get; set; }
        public Channels.ClientModel Client { get; set; }
    }
}