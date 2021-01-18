using System;
using Newtonsoft.Json.Linq;
using static Common.Packets.PacketFactory;

namespace Common.Packets
{

    /// <summary>
    /// A class to represent packages of data to be sent over the network between channels.
    /// ----------------
    /// Packet Structure
    /// ----------------
    ///
    /// Description   -> | bodyLength | encyptionData |   payload    |
    /// Size in bytes -> |     4      |      256      |  bodyLength  |
    ///
    /// bodyLength is a 32 bit integer
    /// encyptionData contains the key/iv pair requred to decrypt the paylood.
    /// encyptionData is encrypted with the user's public key
    /// payload contains: dataId, userId, body
    /// ------------------------------------------------
    /// </summary>
    public class Packet
    {
        #region Public Members

        public byte[] salt;
        public DataID dataID;
        public uint userID;
        public JObject body;

        #endregion

        #region Methods

        /// <summary>
        /// A Packet constructor for creating new packets
        /// </summary>
        /// <param name="dataID">The type of data stored in the Packet</param>
        /// <param name="userID">The user ID of the user creating the Packet (or the user who the Packet is for in the case of the server)</param>
        /// <param name="body">The data</param>
        public Packet(DataID dataID, uint userID, JObject body)
        {
            this.dataID = dataID;
            this.userID = userID;
            this.body = body;
        }

        #endregion
    }

    /// <summary>
    /// A class for passing packets between event handlers.
    /// </summary>
    public class PacketEventArgs : EventArgs
    {
        public Packet Packet { get; set; }
    }
}