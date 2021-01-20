using Common.Helpers;
using Common.Packets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

namespace Common.Channels
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
        private int connectAttempts; //The maximum amount of handshakes to attempt before aborting
        private EncryptionConfig.Strength strength; //Strength of encryption being used on the ClientChannel
        private int largePackets = 0; //Number of datagrams that were too large for the buffer size
        private int packetCount; //Number of packets received in total
        private double packetLossThresh; //Threshold to alert user about significant packet loss
        private EndPoint server; //Represents the endpoint of the server
        private IPAddress serverAdd; //Server IP
        private ManualResetEvent connected; //An event to represent if a connection has been made to the server when using TCP

        #endregion

        #region Methods

        /// <summary>
        /// The ClientChannel constructor
        /// </summary>
        /// <param name="bufferSize">The size to allocate to the channel's buffer (in bytes)</param>
        /// <param name="address">The IP address of the server to connect to</param>
        /// <param name="port">The port the server is hosting on</param>
        /// <param name="connectAttempts">The maximum amount of handshakes to attempt before aborting</param>
        /// <param name="strength">The level of encryption used by the channel</param>
        /// <param name="packetLossThresh">The threshold of packet loss</param>
        public ClientChannel(int bufferSize, IPAddress address, int port, EncryptionConfig.Strength strength, int connectAttempts = 3, double packetLossThresh = 0.05) : base(bufferSize, address, port)
        {
            this.connectAttempts = connectAttempts;
            this.strength = strength;
            this.packetLossThresh = packetLossThresh;
            PacketFactory.SetValues();
            serverAdd = address;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            server = (EndPoint)new IPEndPoint(serverAdd, TCP_PORT);
            connected = new ManualResetEvent(false);
            socket.BeginConnect(server, new AsyncCallback((IAsyncResult ar) => {
                socket.EndConnect(ar);
                connected.Set();
            }), null);
            connected.WaitOne();
        }        

        /// <summary>
        /// A method to start the ClientChannel
        /// </summary>
        public override void Start()
        {
            threads.Add(ThreadHelper.GetECThread(ctoken, Heartbeat));

            threads.Add(ThreadHelper.GetECThread(ctoken, () =>
            {
                Packet packet;
                bool packetAvailable;
                lock (outPackets)
                    packetAvailable = outPackets.TryDequeue(out packet);
                if (packetAvailable)
                    SendPacket(packet);
            })); //Send packets

            threads.Add(ThreadHelper.GetECThread(ctoken, () =>
            {
                if (!listening)
                {
                    listening = true;
                    try
                    {
                        lock (server)
                        {
                            if (socket.ProtocolType == ProtocolType.Udp)
                                socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref server, new AsyncCallback(ReceiveUDPCallback), null);
                            else
                                socket.BeginReceive(TCPHeaderBuffer, 0, HEADER_SIZE, SocketFlags.None, new AsyncCallback(ReceiveTCPCallback), null);
                        }
                        
                    }
                    catch (SocketException)
                    {
                        largePackets++;
                    }
                    packetCount++;
                }
            })); //Listen

            threads.Add(ThreadHelper.GetECThread(ctoken, () =>
            {
                if (packetCount > 10 && largePackets / packetCount > packetLossThresh)
                {
                    Console.WriteLine("Your buffer size is causing significant packet loss. Please consider increasing it.");
                }
                Thread.Sleep(30000);
            })); //Watch packet loss

            threads.Add(ThreadHelper.GetECThread(ctoken, () =>
            {
                byte[] packetBytes;
                bool packetAvailable;
                lock (inPackets)
                    packetAvailable = inPackets.TryDequeue(out packetBytes);
                if (packetAvailable)
                {
                    var inPacket = PacketFactory.BuildPacket(packetBytes);
                    if (inPacket.dataID == PacketFactory.DataID.Heartbeat)
                    {
                        OnDispatch(inPacket);
                        lock (hbLock)
                            receivedHB = true;
                    }
                    else OnDispatch(inPacket);
                }
            })); //Dispatch

            int attempts = 0;
            while (true)
            {
                attempts++;
                if (Handshake())
                {
                    PacketFactory.encCfg.captureSalts = false;
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
        /// A method to change the protocol used by the ClientChannel
        /// </summary>
        /// <param name="protocol">The protocol to change to</param>
        public void ChangeProtocol(ProtocolType protocol)
        {
            if (socket.ProtocolType != protocol)
            {
                lock (socket)
                {
                    if (protocol == ProtocolType.Udp)
                    {
                        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                        server = (EndPoint)new IPEndPoint(serverAdd, UDP_PORT);
                    }
                    else
                    {
                        connected.Reset();
                        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        server = (EndPoint)new IPEndPoint(serverAdd, TCP_PORT);
                        socket.BeginConnect(server, new AsyncCallback((IAsyncResult ar) => {
                            socket.EndConnect(ar);
                            connected.Set();
                        }), null);
                        connected.WaitOne();
                    }
                }
            }
        }

        /// <summary>
        /// A method to perform a handshake with the server
        /// </summary>
        /// <returns>true if the handshake was successfull of false otherwise</returns>
        private bool Handshake()
        {
            Packet outPacket;
            Packet inPacket;
            JObject body;
            byte[] signature;
            ManualResetEvent complete = new ManualResetEvent(false);
            bool failed = false;
            Console.WriteLine($"Connecting to {server} ...");
            PacketFactory.InitEncCfg(EncryptionConfig.Strength.Strong);
            PacketFactory.encCfg.useCrpyto = false;
            PacketFactory.encCfg.captureSalts = true;
            body = new JObject();
            body.Add(PacketFactory.bodyToString[PacketFactory.BodyTag.Strength], Convert.ToBase64String(BitConverter.GetBytes((int)strength)));
            outPacket = new Packet(PacketFactory.DataID.Hello, userID, body);
            List<PacketFactory.DataID> expectedDataList = new List<PacketFactory.DataID> { PacketFactory.DataID.Ack, PacketFactory.DataID.Hello, PacketFactory.DataID.Info, PacketFactory.DataID.Signature };
            Queue<PacketFactory.DataID> expectedData = new Queue<PacketFactory.DataID>(expectedDataList);

            void HandshakeRecursive(IAsyncResult ar)
            {
                try
                { 
                    socket.EndReceiveFrom(ar, ref server);
                    inPacket = PacketFactory.BuildPacket(dataStream);
                    dataStream = new byte[bufferSize];

                    if (inPacket.dataID != expectedData.Dequeue())
                    {
                        complete.Set();
                        failed = true;
                    }

                    switch (inPacket.dataID)
                    {
                        case PacketFactory.DataID.Ack:
                            body = new JObject();
                            body.Add(PacketFactory.bodyToString[PacketFactory.BodyTag.Key], ObjectConverter.GetJObject(PacketFactory.encCfg.pub));
                            outPacket = new Packet(PacketFactory.DataID.Info, userID, body);
                            break;
                        case PacketFactory.DataID.Hello:
                            string serverParamString = inPacket.body.GetValue(PacketFactory.bodyToString[PacketFactory.BodyTag.Key]).ToString();
                            PacketFactory.encCfg.recipient = JsonConvert.DeserializeObject<RSAParameters>(serverParamString);
                            PacketFactory.encCfg.useCrpyto = true;
                            outPacket = new Packet(PacketFactory.DataID.Ack, userID, new JObject()); //Not null so that salt can be added to it
                            break;
                        case PacketFactory.DataID.Info:
                            userID = Convert.ToUInt32(inPacket.body.GetValue(PacketFactory.bodyToString[PacketFactory.BodyTag.ID]).ToString());
                            PacketFactory.encCfg.captureSalts = false;
                            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                            {
                                rsa.ImportParameters(PacketFactory.encCfg.priv);
                                signature = rsa.SignData(PacketFactory.outgoingSalts.ToArray(), SHA512.Create());
                            }
                            body = new JObject();
                            body.Add(PacketFactory.bodyToString[PacketFactory.BodyTag.Signature], Convert.ToBase64String(signature));
                            outPacket = new Packet(PacketFactory.DataID.Signature, userID, body);
                            break;
                        case PacketFactory.DataID.Signature:
                            string sigStr = inPacket.body.GetValue(PacketFactory.bodyToString[PacketFactory.BodyTag.Signature]).ToString();
                            if (sigStr == FAILURE)
                            {
                                complete.Set();
                                failed = true;
                            }
                            signature = Convert.FromBase64String(sigStr);

                            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                            {
                                rsa.ImportParameters(PacketFactory.encCfg.recipient);
                                if (!rsa.VerifyData(PacketFactory.incomingSalts.ToArray(), SHA512.Create(), signature))
                                {
                                    complete.Set();
                                    failed = true;
                                }
                            }
                            complete.Set();
                            break;
                        default:
                            break;
                    }

                    if (!complete.WaitOne(0))
                    {
                        SendPacket(outPacket);
                        socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref server, new AsyncCallback(HandshakeRecursive), null);
                    }
                }
                catch (SocketException)
                {
                    complete.Set();
                    failed = true;
                }
            }

            SendPacket(outPacket);
            PacketFactory.InitEncCfg(strength);
            PacketFactory.encCfg.useCrpyto = false;
            PacketFactory.encCfg.captureSalts = true;
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(PacketFactory.encCfg.RSA_KEY_BITS))
            {
                PacketFactory.encCfg.pub = rsa.ExportParameters(false);
                PacketFactory.encCfg.priv = rsa.ExportParameters(true);
            }
            socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref server, new AsyncCallback(HandshakeRecursive), null);
            complete.WaitOne();
            if (!failed)
            {
                Console.WriteLine("Server handshake successfull.\nConnection established.");
                return true;
            }
            else
            {
                Console.WriteLine("Server handshake failed, re-attempting...");
                return false;
            }
        }
                
        /// <summary>
        /// A method to send heartbeats to the server and check for heartbeats from the server
        /// </summary>
        private protected override void Heartbeat()
        {
            var heartbeat = new Packet(PacketFactory.DataID.Heartbeat, userID, null);
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
        private protected override void SendPacket(Packet packet)
        {
            byte[] data = PacketFactory.GetDataStream(packet);
            if (socket.ProtocolType == ProtocolType.Udp)
                socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, server, new AsyncCallback((IAsyncResult ar) => { socket.EndSendTo(ar); }), null);
            else
            {
                ManualResetEvent sent = new ManualResetEvent(false);
                byte[] header = BitConverter.GetBytes(data.Length);
                socket.BeginSend(header, 0, header.Length, 0, new AsyncCallback((IAsyncResult ar) => { 
                    socket.EndSend(ar);
                    sent.Set();
                }), null);
                sent.WaitOne();
                socket.BeginSend(data, 0, data.Length, 0, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);
            }
        }

        /// <summary>
        /// The callback to run when TCP packets are recieved from the server
        /// </summary>
        /// <param name="ar">An object representing the result of the asynchoronous task</param>
        private protected override void ReceiveTCPCallback(IAsyncResult ar)
        {
            int bytesRead = socket.EndReceive(ar);
            ManualResetEvent received = new ManualResetEvent(false);
            int bytesToRead = BitConverter.ToInt32(TCPHeaderBuffer);
            if (bytesRead > 0)
            {
                try
                {
                    socket.BeginReceive(dataStream, 0, bytesToRead, SocketFlags.None, new AsyncCallback((IAsyncResult ar) => { 
                        socket.EndReceive(ar);
                        received.Set();
                    }), null);
                    received.WaitOne();
                    lock (inPackets)
                        inPackets.Enqueue((byte[])dataStream.Clone());
                    dataStream = new byte[bufferSize];
                    TCPHeaderBuffer = new byte[HEADER_SIZE];
                    listening = false;
                }
                catch (ObjectDisposedException) { }
            }
        }

        /// <summary>
        /// The callback to run when datagrams are recieved from the server
        /// </summary>
        /// <param name="ar">An object representing the result of the asynchoronous task</param>
        private protected override void ReceiveUDPCallback(IAsyncResult ar)
        {
            try
            {
                socket.EndReceiveFrom(ar, ref server);
                lock (inPackets)
                    inPackets.Enqueue((byte[])dataStream.Clone());
                dataStream = new byte[bufferSize];
                listening = false;
            }
            catch (ObjectDisposedException) { }            
        }

        #endregion
    }
}
