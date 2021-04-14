using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Common.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Common.Packets
{
    //Used to identify the type of data the packet contains
    public enum DataID
    {
        Command, //The packet is a command/request from the user to the server
        Ack, //The packet is an acknowledgment packet
        Info, //The packet contains information from the server to the user
        Signature, //The packet contains a verification signature
        Heartbeat, //The packet is a heartbeat
        AV, //The packet conatains AudioVisual frames
        Hello, //The packet is the first packet of a handshake
        Status //The packet contains a status value
    }
   
    /// <summary>
    /// A class for creating and managing Packets
    /// </summary>
    public class PacketFactory : IDisposable
    {
        #region Private Members

        private const int HEADER_SIZE = 4; //bodyLength is a 32 bit integer        

        private static JsonSerializer serializer; //Used for serializing JObjects
        private static RNGCryptoServiceProvider rngCsp; //Secure random generator used for generating body salts
        
        private bool disposedValue;

        //Used to tag elements in the payload
        private enum PayloadTag
        {
            DataID, //Used to identify that an element in the payload specifies the type of data in the Packet
            UserID, //Used to identify that an element in the payload specifies the user who created the Packet (or the user who the Packet is for in the case of the server)
            Body, //Used to identify that an element in the payload is the body of the Packet
        }

        //Used to tag elements in the payload body
        private enum BodyTag
        {
            Salt
        }
        
        #endregion

        #region Public Members

        public EncryptionConfig encCfg; //The settings to use for encrypting and decrpyting packets
        public List<byte> incomingSalts;
        public List<byte> outgoingSalts;

        #endregion

        #region Methods

        public PacketFactory()
        {
            incomingSalts = new List<byte>();
            outgoingSalts = new List<byte>();
            if (serializer == null)
                rngCsp = new RNGCryptoServiceProvider();
            if (serializer == null)
                serializer = ObjectConverter.GetJsonSerializer();
        }

        /// <summary>
        /// A method to initialise the static EncryptionConfig object used by the classs
        /// </summary>
        /// <param name="strength">The strength of encryption</param>
        public void InitEncCfg(EncryptionConfig.Strength strength)
        {
            encCfg = new EncryptionConfig(strength);
        }
        
        /// <summary>
        /// A Packet constructor for rebuilding packets from an existing data source.
        /// </summary>
        /// <param name="dataStream">the byte representation of the packet to build</param>
        public Packet BuildPacket(byte[] dataStream)
        {
            int bodyLength = BitConverter.ToInt32(dataStream.Take(HEADER_SIZE).ToArray());
            byte[] e_aesData = dataStream.Skip(HEADER_SIZE).Take(encCfg.RSA_OUTPUT).ToArray();
            byte[] payloadBytes = dataStream.Skip(HEADER_SIZE + encCfg.RSA_OUTPUT).Take(bodyLength).ToArray();

            if (encCfg.useCrypto)
            {
                using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
                {
                    aes.KeySize = encCfg.AES_KEY_BITS;
                    aes.Padding = PaddingMode.PKCS7;

                    using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(encCfg.RSA_KEY_BITS))
                    {
                        rsa.ImportParameters(encCfg.priv);
                        byte[] d_aesData = rsa.Decrypt(e_aesData, RSAEncryptionPadding.Pkcs1);
                        aes.Key = d_aesData.Take(encCfg.AES_KEY_LENGTH).ToArray();
                        aes.IV = d_aesData.Skip(encCfg.AES_KEY_LENGTH).Take(encCfg.AES_IV_LENGTH).ToArray();
                    }

                    using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                        payloadBytes = DoCrypto(payloadBytes, decryptor);
                }
            }

            var payload = JObject.Parse(Encoding.UTF8.GetString(payloadBytes));
            string dataIDString = payload.GetValue(PayloadTag.DataID.ToString()).ToString();
            string userIDString = payload.GetValue(PayloadTag.UserID.ToString()).ToString();
            string bodyString_b64 = payload.GetValue(PayloadTag.Body.ToString()).ToString();
            DataID dataID = (DataID)Convert.ToInt32(dataIDString);
            uint userID = Convert.ToUInt32(userIDString);
            string bodyString = Encoding.UTF8.GetString(Convert.FromBase64String(bodyString_b64));
            JObject body = null;
            if (bodyString != "null")
            {
                body = JObject.Parse(bodyString);
                if (encCfg.captureSalts)
                    incomingSalts.AddRange(Convert.FromBase64String(body.GetValue(BodyTag.Salt.ToString()).ToString()));
            }

            Packet packet = new Packet(dataID, userID);
            packet.body = body;
            return packet;
        }

        /// <summary>
        /// A method to get the representation of a packet in the form of a byte array so that
        /// the packet can be sent over the network and reconstructed on delivery.
        /// </summary>
        /// <returns>The byte representation of the packet.</returns>
        public byte[] GetDataStream(Packet packet)
        {
            string dataID = ((int)packet.dataID).ToString();
            string userID = packet.userID.ToString();
            if (encCfg.captureSalts)
                SaltBody(packet);
            var sb = new StringBuilder();
            var sw = new StringWriter(sb);
            serializer.Serialize(sw, packet.body);
            var bodyBytes = Encoding.UTF8.GetBytes(sb.ToString());

            var json = new JObject();
            json.Add(PayloadTag.DataID.ToString(), dataID);
            json.Add(PayloadTag.UserID.ToString(), userID);
            json.Add(PayloadTag.Body.ToString(), Convert.ToBase64String(bodyBytes));
            sb = new StringBuilder();
            sw = new StringWriter(sb);
            serializer.Serialize(sw, json);
            byte[] payload = Encoding.UTF8.GetBytes(sb.ToString());

            byte[] e_aesData = new byte[encCfg.RSA_OUTPUT];

            if (encCfg.useCrypto)
            {
                using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
                {
                    aes.KeySize = encCfg.AES_KEY_BITS;
                    aes.Padding = PaddingMode.PKCS7;

                    using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                    {
                        rsa.ImportParameters(encCfg.recipient);
                        rsa.KeySize = encCfg.RSA_KEY_BITS;
                        byte[] d_aesData = aes.Key.Concat(aes.IV).ToArray();
                        e_aesData = rsa.Encrypt(d_aesData, RSAEncryptionPadding.Pkcs1);
                    }

                    using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                        payload = DoCrypto(payload, encryptor);
                }
            }

            List<byte> dataStream = new List<byte>();
            dataStream.AddRange(BitConverter.GetBytes(payload.Length));
            dataStream.AddRange(e_aesData);
            dataStream.AddRange(payload);

            return dataStream.ToArray();
        }

        /// <summary>
        /// A method to generate a secure random salt value and add it to a body.
        /// </summary>
        /// <param name="body">The JObject representing the body</param>
        private void SaltBody(Packet packet)
        {
            packet.salt = new byte[encCfg.SALT_SIZE];
            rngCsp.GetBytes(packet.salt);
            if (packet.body == null)
                packet.body = new JObject();
            packet.body.Add(BodyTag.Salt.ToString(), Convert.ToBase64String(packet.salt));
            outgoingSalts.AddRange(packet.salt);
        }
        
        /// <summary>
        /// A method to perform a symmetric cryptographic operation on an array of bytes.
        /// </summary>
        /// <param name="dataStream">The array of bytes to perform the operation on.</param>
        /// <param name="transform">The operation to perform</param>
        /// <returns></returns>
        private byte[] DoCrypto(byte[] dataStream, ICryptoTransform transform)
        {
            using (var ms = new MemoryStream())
            {
                using (var cryptoStream = new CryptoStream(ms, transform, CryptoStreamMode.Write))
                {
                    try
                    {
                        cryptoStream.Write(dataStream, 0, dataStream.Length);
                        cryptoStream.FlushFinalBlock();
                        return ms.ToArray();
                    }
                    catch (CryptographicException) { throw; }
                }
            }
        }

        #region IDisposable Implementation

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    rngCsp.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #endregion
    }
}