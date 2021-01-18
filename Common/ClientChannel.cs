using Common.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

namespace Common
{
    /// <summary>
    /// A class used by clients to communicate over the network with the server, and with other users via the server
    /// </summary>
    public class ClientChannel : Channel
    {
        #region Private Memebers

        private uint userID = 1; //Default userID, this value is kept until the server assigns the channel a new userID
        private bool receivedHB = false; //A boolean used for checking if the channel has received a hearbeat or not
        private int missedHBs = 0; //A counter of how many hearbeats have been missed
        private bool listening = false; //A boolean to check if the channel is currently listening on the socket
        private ManualResetEvent initial; //An event to make sure a handshake if full attempted before a value is returned from Handshake()
        private int connectAttempts; //The maximum amount of handshakes to attempt before aborting
        private List<byte> outSaltList; //The list of salts from packets sent to the server
        private List<byte> inSaltList; //The list of salts from packets received from the server
        private bool handshaking = true; //A boolean to check if the channel is currently performing a handshake

        #endregion

        #region Methods

        /// <summary>
        /// The ClientChannel constructor
        /// </summary>
        /// <param name="bufferSize">The size to allocate to the channel's buffer (in bytes)</param>
        /// <param name="address">The IP address of the server to connect to</param>
        /// <param name="port">The port the server is hosting on</param>
        /// <param name="connectAttempts">The maximum amount of handshakes to attempt before aborting</param>
        public ClientChannel(int bufferSize, IPAddress address, int port, int connectAttempts) : base(bufferSize, address, port)
        {
            initial = new ManualResetEvent(false);
            this.connectAttempts = connectAttempts;
            Packet.SetValues();
            outSaltList = new List<byte>();
            inSaltList = new List<byte>();
        }        

        /// <summary>
        /// A method to start the ClientChannel
        /// </summary>
        public override void Start()
        {
            threads.Add(ThreadHelper.GetECThread(ctoken, Heartbeat));

            threads.Add(ThreadHelper.GetECThread(ctoken, () => {
                Packet packet;
                bool packetAvailable;
                lock (outPackets)
                    packetAvailable = outPackets.TryDequeue(out packet);
                if (packetAvailable)
                    SendData(packet);
            }));

            threads.Add(ThreadHelper.GetECThread(ctoken, () => {
                if (!listening)
                {
                    listening = true;
                    _ = socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref endpoint, new AsyncCallback(ReceiveData), null);
                }
            }));

            threads.Add(ThreadHelper.GetECThread(ctoken, () => {
                byte[] packetBytes;
                bool packetAvailable;
                lock (inPackets)
                    packetAvailable = inPackets.TryDequeue(out packetBytes);
                if (packetAvailable)
                {
                    var inPacket = new Packet(packetBytes);
                    if (inPacket.dataID == Packet.DataID.Heartbeat)
                    {
                        OnDispatch(inPacket);
                        lock (hbLock)
                            receivedHB = true;
                    }
                    else OnDispatch(inPacket);
                }
            }));

            int attempts = 0;
            while (true)
            {
                attempts++;
                if (Handshake())
                {
                    foreach (var thread in threads)
                        thread.Start();
                    break;
                }
                if (attempts == connectAttempts)
                {
                    Console.WriteLine($"Failed to connect {attempts} times, aborting");
                    break;
                }
            }            
        }

        /// <summary>
        /// A method to perform a handshake with the server
        /// </summary>
        /// <returns>true if the handshake was successfull of false otherwise</returns>
        private bool Handshake()
        {
            handshaking = true;
            initial.Reset();
            try {
                Console.WriteLine($"Connecting to {endpoint}...");

                RSAParameters priv;
                RSAParameters pub;
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(Packet.RSA_KEY_BITS))
                {
                    pub = rsa.ExportParameters(false);
                    priv = rsa.ExportParameters(true);
                }

                JObject body = new JObject();
                body.Add(Packet.bodyToString[Packet.BodyTag.Key], ObjectConverter.GetJObject(pub));
                var packet = new Packet(Packet.DataID.Hello, userID, body);
                SendData(packet);

                _ = socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref endpoint, new AsyncCallback((IAsyncResult ar) =>
                {
                    _ = socket.EndReceiveFrom(ar, ref endpoint);
                    var inPacket = new Packet(dataStream, ref inSaltList);
                    dataStream = new byte[bufferSize];
                    if (inPacket.dataID != Packet.DataID.Hello)
                        throw new HandshakeException();

                    string serverParamString = inPacket.body.GetValue(Packet.bodyToString[Packet.BodyTag.Key]).ToString();
                    RSAParameters serverParams = JsonConvert.DeserializeObject<RSAParameters>(serverParamString);
                    Packet.SetRSAParameters(serverParams, priv);
                    var outPacket = new Packet(Packet.DataID.Ack, userID, Packet.EmptyBody);
                    SendData(outPacket);

                    _ = socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref endpoint, new AsyncCallback((IAsyncResult ar) =>
                    {
                        _ = socket.EndReceiveFrom(ar, ref endpoint);
                        var inPacket = new Packet(dataStream, ref inSaltList);
                        dataStream = new byte[bufferSize];
                        if (inPacket.dataID != Packet.DataID.Info)
                            throw new HandshakeException();

                        userID = Convert.ToUInt32(inPacket.body.GetValue(Packet.bodyToString[Packet.BodyTag.ID]).ToString());
                        
                        byte[] signature;
                        using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                        {
                            rsa.ImportParameters(priv);
                            signature = rsa.SignData(outSaltList.ToArray(), SHA512.Create());
                        }

                        var body = new JObject();
                        body.Add(Packet.bodyToString[Packet.BodyTag.Signature], Convert.ToBase64String(signature));
                        var outPacket = new Packet(Packet.DataID.Signature, userID, body);
                        SendData(outPacket);

                        _ = socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref endpoint, new AsyncCallback((IAsyncResult ar) =>
                        {
                            _ = socket.EndReceiveFrom(ar, ref endpoint);
                            var inPacket = new Packet(dataStream);
                            dataStream = new byte[bufferSize];
                            if (inPacket.dataID != Packet.DataID.Signature)
                                throw new HandshakeException();

                            byte[] serverSig = Convert.FromBase64String(inPacket.body.GetValue(Packet.bodyToString[Packet.BodyTag.Signature]).ToString());
                            if (Convert.ToBase64String(serverSig) == FAILURE)
                                throw new HandshakeException();

                            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                            {
                                rsa.ImportParameters(serverParams);
                                if (!rsa.VerifyData(inSaltList.ToArray(), SHA512.Create(), serverSig))
                                    throw new HandshakeException();
                            }                              

                            initial.Set();
                        }), null);

                    }), null);                
                
                }), null);                
            }
            catch (HandshakeException)
            {
                handshaking = false;
                Console.WriteLine("Server handshake failed, re-attempting...");
                return false;
            }
            
            initial.WaitOne();
            handshaking = false;
            Console.WriteLine("Server handshake successfull.\nConnection established.");
            return true;
        }

        /// <summary>
        /// A method to send heartbeats to the server and check for heartbeats from the server
        /// </summary>
        private protected override void Heartbeat()
        {
            var heartbeat = new Packet(Packet.DataID.Heartbeat, userID, Packet.EmptyBody);
            lock (outPackets)
                outPackets.Enqueue(heartbeat);            
            Thread.Sleep(5000);
            lock (hbLock)
            {
                if (receivedHB)
                {
                    receivedHB = false;
                    missedHBs = 0;
                }
                else
                {
                    missedHBs += 1;
                    if (missedHBs == 2)
                    {
                        Dispose();
                        Console.WriteLine("Connection to server has died");
                    }
                }
            }
        }

        /// <summary>
        /// A method used to send Packets from the Client to the server
        /// </summary>
        /// <param name="packet">The packet to send</param>
        private protected override void SendData(Packet packet)
        {
            byte[] dataStream;
            if (handshaking)
                dataStream = packet.GetDataStream(ref outSaltList);
            else
                dataStream = packet.GetDataStream();
            socket.BeginSendTo(dataStream, 0, dataStream.Length, SocketFlags.None, endpoint, new AsyncCallback((IAsyncResult ar) => { socket.EndSendTo(ar); }), null);            
        }

        /// <summary>
        /// The callback to run when packets are recieved from the server
        /// </summary>
        /// <param name="ar">An object representing the result of the asynchoronous task</param>
        private protected override void ReceiveData(IAsyncResult ar)
        {
            _ = socket.EndReceiveFrom(ar, ref endpoint);
            lock (inPackets)
                inPackets.Enqueue((byte[])dataStream.Clone());
            dataStream = new byte[bufferSize];
            listening = false;
        }

        #endregion
    }
}
