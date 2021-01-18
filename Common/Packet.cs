using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Common.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Common
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
        #region Private Members

        private const int HEADER_SIZE = 4; //bodyLength is a 32 bit integer
        private const int AES_KEY_BITS = 256; //AES key size (in bits)
        private const int AES_KEY_LENGTH = 32; //AES key size (in bytes)
        private const int AES_IV_LENGTH = 16; //AES iv size (in bytes)        
        private const int RSA_OUTPUT = 256; //length of encryptionData (size of output arr from RSA encryption) (in bytes)
        private const int SALT_SIZE = 8; //size of body salt (in bytes)

        private byte[] salt; //The body salt of the packet

        private static JsonSerializer serializer; //Used for serializing JObjects
        private static bool useCrypto = false; //Used to check if packets should be encrypted
        private static RSAParameters encParams; //Public key for asymmetric encryption
        private static RSAParameters decParams; //Private key for asymmetric decryption
        private static Dictionary<DataID, string> dataToString; //Dictionary to convert DataID values to their bytestring representation
        private static Dictionary<PayloadTag, string> payloadToString; //Dictionary to convert PayloadTag values to their bytestring representation
        private static RNGCryptoServiceProvider rngCsp; //Secure random generator used for generating body salts


        //Used to tag elements in the payload
        private enum PayloadTag
        {
            DataID, //Used to identify that an element in the payload specifies the type of data in the Packet
            UserID, //Used to identify that an element in the payload specifies the user who created the Packet (or the user who the Packet is for in the case of the server)
            Body, //Used to identify that an element in the payload is the body of the Packet
        }

        #endregion

        #region Public Members

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
            Null
        }

        //Used to tag elements in the body (of the payload)
        public enum BodyTag
        {
            Key, //The element is an asymmetric public key
            Signature, //The element is a verification signature
            ID, //The element is the ID number of the user
            Salt // The element is a body salt
        }

        public static Dictionary<BodyTag, string> bodyToString; //Dictionary to convert BodyTag values to their bytestring representation
        public const int RSA_KEY_BITS = 2048; //RSA key size (in bits)

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
            serializer = ObjectConverter.GetJsonSerializer();
        }
        /// <summary>
        /// A Packet constructor for rebuilding packets from an existing data source.
        /// </summary>
        /// <param name="dataStream">The Packet.GetDataStream() output of the original Packet</param>
        public Packet(byte[] dataStream)
        {
            int bodyLength = BitConverter.ToInt32(dataStream.Take(HEADER_SIZE).ToArray());
            byte[] e_aesData = dataStream.Skip(HEADER_SIZE).Take(RSA_OUTPUT).ToArray();
            byte[] payloadBytes = dataStream.Skip(HEADER_SIZE + RSA_OUTPUT).Take(bodyLength).ToArray();

            if (useCrypto)
            {
                using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
                {
                    aes.KeySize = AES_KEY_BITS;
                    aes.Padding = PaddingMode.PKCS7;

                    using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(RSA_KEY_BITS))
                    {
                        rsa.ImportParameters(decParams);
                        byte[] d_aesData = rsa.Decrypt(e_aesData, RSAEncryptionPadding.Pkcs1);
                        aes.Key = d_aesData.Take(AES_KEY_LENGTH).ToArray();
                        aes.IV = d_aesData.Skip(AES_KEY_LENGTH).Take(AES_IV_LENGTH).ToArray();
                    }

                    using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                        payloadBytes = DoCrypto(payloadBytes, decryptor);
                }
            }

            var payload = JObject.Parse(Encoding.UTF8.GetString(payloadBytes));
            string dataIDString = payload.GetValue(payloadToString[PayloadTag.DataID]).ToString();
            string userIDString = payload.GetValue(payloadToString[PayloadTag.UserID]).ToString();
            string bodyString_b64 = payload.GetValue(payloadToString[PayloadTag.Body]).ToString();
            dataID = (DataID)BitConverter.ToInt32(Convert.FromBase64String(dataIDString));
            userID = BitConverter.ToUInt32(Convert.FromBase64String(userIDString));
            string bodyString = Encoding.UTF8.GetString(Convert.FromBase64String(bodyString_b64));
            if (bodyString != "null")
            {
                body = JObject.Parse(bodyString);
                if (body.GetValue(bodyToString[BodyTag.Salt]).ToString() != null)
                    salt = Convert.FromBase64String(body.GetValue(bodyToString[BodyTag.Salt]).ToString());
                else
                    salt = null;
            }

            serializer = ObjectConverter.GetJsonSerializer();
        }
        /// <summary>
        /// A Packet constructor for rebuilding packets from an existing data source, and storing their body salts for building signatures
        /// </summary>
        /// <param name="dataStream">The Packet.GetDataStream() output of the original Packet</param>
        /// <param name="saltList">A reference to the list in which the body salt should be appened to</param>
        public Packet(byte[] dataStream, ref List<byte> saltList) : this(dataStream)
        {
            if (salt != null)
                saltList.AddRange(salt);
        }

        /// <summary>
        /// A method to initialise the construcatble static attribues of the Packet class.
        /// Should be called before any packets are made.
        /// </summary>
        public static void SetValues()
        {
            dataToString = new Dictionary<DataID, string>();
            payloadToString = new Dictionary<PayloadTag, string>();
            bodyToString = new Dictionary<BodyTag, string>();
            rngCsp = new RNGCryptoServiceProvider();

            foreach (DataID data in Enum.GetValues(typeof(DataID)))
            {
                dataToString.Add(data, ObjectConverter.EnumToByteString(data));
            }
            foreach (PayloadTag payload in Enum.GetValues(typeof(PayloadTag)))
            {
                payloadToString.Add(payload, ObjectConverter.EnumToByteString(payload));
            }
            foreach (BodyTag body in Enum.GetValues(typeof(BodyTag)))
            {
                bodyToString.Add(body, ObjectConverter.EnumToByteString(body));
            }
        }

        /// <summary>
        /// A method to set the aymmetric keys which will be use for encryption.
        /// Packets will not be encrytped or decrypted until this method has been called.
        /// </summary>
        /// <param name="eparams">The asymmetric public key to use for encryption<./param>
        /// <param name="dparams">The asymmetric private key to use for decryption.</param>
        public static void SetRSAParameters(RSAParameters eparams, RSAParameters dparams)
        {
            encParams = eparams;
            decParams = dparams;
            useCrypto = true;
        }

        /// <summary>
        /// A method to dispose of any static IDisposable objects created in the class and nullify large fields.
        /// </summary>
        public static void Cleanup()
        {
            dataToString = null;
            payloadToString = null;
            bodyToString = null;
            rngCsp.Dispose();
        }

        /// <summary>
        /// A method to get the representation of a packet in the form of a byte array so that
        /// the packet can be sent over the network and reconstructed on delivery.
        /// </summary>
        /// <returns>The byte array representation of the packet.</returns>
        public byte[] GetDataStream()
        {
            var b_dataID = BitConverter.GetBytes((int)dataID);
            var b_userID = BitConverter.GetBytes(userID);
            SaltBody(body);
            var sb = new StringBuilder();
            var sw = new StringWriter(sb);
            serializer.Serialize(sw, body);
            var bodyBytes = Encoding.UTF8.GetBytes(sb.ToString());

            var json = new JObject();
            json.Add(payloadToString[PayloadTag.DataID], Convert.ToBase64String(b_dataID));
            json.Add(payloadToString[PayloadTag.UserID], Convert.ToBase64String(b_userID));
            json.Add(payloadToString[PayloadTag.Body], Convert.ToBase64String(bodyBytes));
            sb = new StringBuilder();
            sw = new StringWriter(sb);
            serializer.Serialize(sw, json);
            byte[] payload = Encoding.UTF8.GetBytes(sb.ToString());

            byte[] e_aesData = new byte[RSA_OUTPUT];

            if (useCrypto)
            {
                using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
                {
                    aes.KeySize = AES_KEY_BITS;
                    aes.Padding = PaddingMode.PKCS7;

                    using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                    {
                        rsa.ImportParameters(encParams);
                        rsa.KeySize = RSA_KEY_BITS;
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
        /// A method to get the representation of a packet in the form of a byte array so that
        /// the packet can be sent over the network and reconstructed on delivery. It also adds the generated
        /// body salt of the packet to the supplied list for signature creation.
        /// </summary>
        /// <param name="saltList">A reference to the list which the body salt should be added to.</param>
        /// <returns>The byte array representation of the packet.</returns>
        public byte[] GetDataStream(ref List<byte> saltList)
        {            
            byte[] dataStream = GetDataStream();
            saltList.AddRange(salt);
            return dataStream;
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

        /// <summary>
        /// A method to generate a secure random salt value and add it to a body.
        /// </summary>
        /// <param name="body">The JObject representing the body</param>
        private void SaltBody(JObject body)
        {   
            salt = new byte[SALT_SIZE];
            rngCsp.GetBytes(salt);
            if (body != null)
                body.Add(bodyToString[BodyTag.Salt], Convert.ToBase64String(salt));
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