using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Common
{
    // ----------------
    // Packet Structure
    // ----------------

    // Description   -> | bodyLength |dataIdentifier| userIdentifier |  dataBody  |
    // Size in bytes -> |     4      |       4      |       4        | dataHeader |

    public enum DataID
    {
        Command,
        Heartbeat,
        AV,
        Null
    }

    public class Packet
    {
        #region Private Members

        private const int METADATA_SIZE = 4;
        private readonly JsonSerializer serializer;
        private readonly JsonSerializerSettings settings;
        private int bodyLength;

        #endregion

        #region Public Members

        public DataID dataID;
        public int userID;
        public JObject body;
        
        #endregion

        #region Methods

        public Packet(DataID did, int uid)
        {
            bodyLength = 0;
            dataID = did;
            userID = uid;
            body = new JObject();

            settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy
                    {
                        ProcessDictionaryKeys = false
                    }
                }
            };
            settings.PreserveReferencesHandling = PreserveReferencesHandling.None;
            serializer = JsonSerializer.Create(settings);
        }

        public Packet(byte[] dataStream)
        {
            bodyLength = BitConverter.ToInt32(dataStream, 0);
            dataID = (DataID)BitConverter.ToInt32(dataStream, METADATA_SIZE);
            userID = BitConverter.ToInt32(dataStream, 2 * METADATA_SIZE);
            byte[] bodyBytes = dataStream.Skip(3 * METADATA_SIZE).Take(bodyLength).ToArray();
            body = JObject.Parse(Encoding.UTF8.GetString(bodyBytes));

            settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy
                    {
                        ProcessDictionaryKeys = false
                    }
                }
            };
            settings.PreserveReferencesHandling = PreserveReferencesHandling.None;
            serializer = JsonSerializer.Create(settings);
        }

        // Converts the packet into a byte array for sending/receiving 
        public byte[] GetDataStream()
        {
            List<byte> dataStream = new List<byte>();

            var sb = new StringBuilder();
            var sw = new StringWriter(sb);
            serializer.Serialize(sw, body);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(sb.ToString());

            dataStream.AddRange(BitConverter.GetBytes(bodyBytes.Length));
            dataStream.AddRange(BitConverter.GetBytes((int)dataID));
            dataStream.AddRange(BitConverter.GetBytes(userID));
            dataStream.AddRange(bodyBytes);

            return dataStream.ToArray();
        }

        #endregion
    }

    public class PacketEventArgs : EventArgs
    {
        public Packet Packet { get; set; }
    }
}