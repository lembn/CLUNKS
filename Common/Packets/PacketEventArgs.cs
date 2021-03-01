using System;

namespace Common.Packets
{
    /// <summary>
    /// A class for passing packets between event handlers.
    /// </summary>
    public class PacketEventArgs : EventArgs
    {
        public Packet packet;
        public Channels.ClientModel client;

        /// <summary>
        /// PacketEventArgs for ClientChannel
        /// </summary>
        /// <param name="packet">The packet data</param>
        public PacketEventArgs(Packet packet) => this.packet = packet;
        /// <summary>
        /// PacketEventArgs constructor for ServerChannel
        /// </summary>
        /// <param name="packet">The packet data</param>
        /// <param name="client">The client data</param>
        public PacketEventArgs(Packet packet, Channels.ClientModel client)
        {
            this.packet = packet;
            this.client = client;
        }
    }
}