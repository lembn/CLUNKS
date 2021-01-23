using System;
using Newtonsoft.Json.Linq;

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
        public int dataCounter = 0;

        public const string DATA = "data-{0}";
        public static readonly string BODYFIRST = String.Format(DATA, 0);

        #endregion

        #region Methods

        /// <summary>
        /// A Packet constructor for creating new packets
        /// </summary>
        /// <param name="dataID">The type of data stored in the Packet</param>
        /// <param name="userID">The user ID of the user creating the Packet (or the user who the Packet is for in the case of the server)</param>
        /// <param name="body">The data</param>
        public Packet(DataID dataID, uint userID)
        {
            this.dataID = dataID;
            this.userID = userID;
        }

        /// <summary>
        /// A method to add items to the body of a packet
        /// </summary>
        /// <param name="packet">The packet being populated</param>
        /// <param name="data">The data to add</param>
        public void Add<T>(params T[] data)
        {
            if (body == null)
                body = new JObject();

            foreach (T item in data)
            {
                body.Add(String.Format(DATA, dataCounter++), JToken.FromObject(item));
            }

        }

        #endregion
    }
}