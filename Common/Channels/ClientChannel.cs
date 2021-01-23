using Common.Helpers;
using Common.Packets;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
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
    public class ClientChannel : Channel, IDisposable
    {
        #region Private Memebers

        private PacketFactory packetFactory; //The object to use for handling packets
        private EncryptionConfig.Strength strength; //Strength of encryption being used on the ClientChannel
        private AutoResetEvent receiving; //An event used to check if the channel is currently listening on the socket
        private bool receivingHeader = true; //A boolean to represent if a TCP listen is currently reading the header or the actual packet
        private object hbLock; //A lock used for thread synchronisation when processing heartbeats
        private bool receivedHB = false; //A boolean used for checking if the channel has received a hearbeat or not
        private int missedHBs = 0; //A counter of how many hearbeats have been missed
        private Socket socket; //The socket to listen on (and send over)
        private EndPoint server; //Represents the endpoint of the server
        private int bufferSize; //The default buffer size of the channel
        private IPAddress serverIP; //Server IP
        private int connectAttempts; //The maximum amount of handshakes to attempt before aborting
        private ManualResetEvent connected; //An event to represent if a connection has been made to the server when using TCP
        private int largePackets = 0; //Number of datagrams that were too large for the buffer size
        private int packetCount; //Number of packets received in total
        private double packetLossThresh; //Threshold to alert user about significant packet loss
        private DataStream dataStream; //Buffer for incoming data
        private BlockingCollection<Packet> outPackets; //A queue to hold outgoing packets
        private BlockingCollection<Packet> inPackets; //A queue to hold incomging packets

        #endregion
        
        public uint id = NULL_ID;

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
        public ClientChannel(int bufferSize, IPAddress ip, EncryptionConfig.Strength strength, int connectAttempts = 3, double packetLossThresh = 0.05) : base()
        {
            this.connectAttempts = connectAttempts;
            this.strength = strength;
            this.bufferSize = bufferSize;
            this.packetLossThresh = packetLossThresh;
            outPackets = new BlockingCollection<Packet>();
            inPackets = new BlockingCollection<Packet>();
            receiving = new AutoResetEvent(true);
            packetFactory = new PacketFactory();
            hbLock = new object();
            serverIP = ip;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            server = (EndPoint)new IPEndPoint(serverIP, TCP_PORT);
            connected = new ManualResetEvent(false);
            dataStream = new DataStream(bufferSize);
            socket.BeginConnect(server, new AsyncCallback((IAsyncResult ar) => 
            {
                try
                {
                    socket.EndConnect(ar);
                }
                catch (Exception)
                {
                    Console.WriteLine($"There is no CLUNK server at {ip}:{TCP_PORT}");
                    throw new SocketException();
                }
                connected.Set();
            }), null);
            connected.WaitOne();            
        }        

        /// <summary>
        /// A method to start the ClientChannel
        /// </summary>
        public override void Start()
        {
            threads.Add(ThreadHelper.GetECThread(ctoken, () => 
            {
                lock (outPackets)
                    outPackets.Add(new Packet(DataID.Heartbeat, id));
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
                            Disconnect();
                        }
                    }
                }

            })); //Heartbeat

            threads.Add(ThreadHelper.GetECThread(ctoken, () => 
            {
                Packet packet;
                bool packetAvailable;
                lock (outPackets)
                    packetAvailable = outPackets.TryTake(out packet);
                if (packetAvailable)
                    SendPacket(packet);
            })); //Send packets

            threads.Add(ThreadHelper.GetECThread(ctoken, () => 
            {
                receiving.WaitOne();
                try
                {
                    lock (socket)
                    {
                        if (socket.ProtocolType == ProtocolType.Tcp)
                            socket.BeginReceive(dataStream.New(), 0, HEADER_SIZE, SocketFlags.None, new AsyncCallback((IAsyncResult ar) => {
                                receivingHeader = true;
                                ReceiveTCPCallback(ar);
                            }), dataStream);
                        else
                            socket.BeginReceiveFrom(dataStream.New(), 0, bufferSize, SocketFlags.None, ref server, new AsyncCallback(ReceiveUDPCallback), null);
                    }
                        
                }
                catch (SocketException)
                {
                    largePackets++;
                }
                packetCount++;
            })); //Receive

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
                Packet packet;
                bool packetAvailable;
                lock (inPackets)
                    packetAvailable = inPackets.TryTake(out packet);
                if (packetAvailable)
                {
                    if (packet.dataID == DataID.Heartbeat)
                    {
                        lock (hbLock)
                            receivedHB = true;
                    }
                    else OnDispatch(packet);
                }
            })); //Dispatch

            int attempts = 0;
            while (true)
            {
                attempts++;
                if (Handshake())
                {
                    packetFactory.encCfg.captureSalts = false;
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
                        server = (EndPoint)new IPEndPoint(serverIP, UDP_PORT);
                    }
                    else
                    {
                        connected.Reset();
                        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        server = (EndPoint)new IPEndPoint(serverIP, TCP_PORT);
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
        /// A method to expose the outPackets queue so that members outside the Channel class can
        /// add packets to be sent.
        /// </summary>
        /// <param name="packet">The packet to be sent</param>
        public void Add(Packet packet) => outPackets.Add(packet);

        /// <summary>
        /// A method to perform a handshake with the server
        /// </summary>
        /// <returns>true if the handshake was successfull of false otherwise</returns>
        private bool Handshake()
        {
            Packet outPacket = null;
            byte[] signature;
            ManualResetEvent complete = new ManualResetEvent(false);
            bool failed = false;
            List<DataID> expectedDataList = new List<DataID> { DataID.Ack, DataID.Hello, DataID.Info, DataID.Signature };
            Queue<DataID> expectedData = new Queue<DataID>(expectedDataList);

            void HandshakeRecursive(IAsyncResult ar, int bytesToRead = 0)
            {
                dataStream = (DataStream)ar.AsyncState;
                int bytesRead = socket.EndReceive(ar);
                if (receivingHeader)
                {
                    bytesToRead = BitConverter.ToInt32(dataStream.Get()) + HEADER_SIZE; //(+ HEADER_SIZE because when we pass the recursive CB we subtract bytesRead from bytesToRead)
                    receivingHeader = false;
                }
                if (bytesToRead - bytesRead > 0)
                    socket.BeginReceive(dataStream.New(), 0, dataStream.bufferSize, SocketFlags.None, new AsyncCallback((IAsyncResult ar) =>
                    {
                        HandshakeRecursive(ar, bytesToRead - bytesRead);
                    }), dataStream);
                else
                {
                    ProcessPacket(packetFactory.BuildPacket(dataStream.Get()));
                }
            }

            void ProcessPacket(Packet inPacket)
            {
                try
                { 
                    if (inPacket.dataID != expectedData.Dequeue())
                    {
                        complete.Set();
                        failed = true;
                    }

                    switch (inPacket.dataID)
                    {
                        case DataID.Ack:
                            outPacket = new Packet(DataID.Info, id);
                            outPacket.Add(ObjectConverter.GetJObject(packetFactory.encCfg.pub));
                            break;
                        case DataID.Hello:
                            string serverParamString = inPacket.body.GetValue(Packet.BODYFIRST).ToString();
                            packetFactory.encCfg.recipient = JsonConvert.DeserializeObject<RSAParameters>(serverParamString);
                            packetFactory.encCfg.useCrpyto = true;
                            outPacket = new Packet(DataID.Ack, id);
                            break;
                        case DataID.Info:
                            id = Convert.ToUInt32(inPacket.body.GetValue(Packet.BODYFIRST).ToString());
                            packetFactory.encCfg.captureSalts = false;
                            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                            {
                                rsa.ImportParameters(packetFactory.encCfg.priv);
                                signature = rsa.SignData(packetFactory.outgoingSalts.ToArray(), SHA512.Create());
                            }
                            outPacket = new Packet(DataID.Signature, id);
                            outPacket.Add(Convert.ToBase64String(signature));
                            break;
                        case DataID.Signature:
                            string sigStr = inPacket.body.GetValue(Packet.BODYFIRST).ToString();
                            if (sigStr == FAILURE)
                            {
                                complete.Set();
                                failed = true;
                            }
                            signature = Convert.FromBase64String(sigStr);

                            outPacket = new Packet(DataID.Status, id);
                            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                            {
                                rsa.ImportParameters(packetFactory.encCfg.recipient);
                                if (rsa.VerifyData(packetFactory.incomingSalts.ToArray(), SHA512.Create(), signature))
                                    outPacket.Add(SUCCESS);
                                else
                                {
                                    failed = true;
                                    outPacket.Add(FAILURE);
                                }
                            }
                            complete.Set();
                            break;
                        default:
                            break;
                    }

                    SendPacket(outPacket);
                    if (!complete.WaitOne(0))
                        socket.BeginReceive(dataStream.New(), 0, HEADER_SIZE, SocketFlags.None, new AsyncCallback((IAsyncResult ar) => {
                            receivingHeader = true;
                            HandshakeRecursive(ar);
                        }), dataStream);
                }
                catch (SocketException)
                {
                    complete.Set();
                    failed = true;
                }
            }

            packetFactory.InitEncCfg(EncryptionConfig.Strength.Strong);
            packetFactory.encCfg.useCrpyto = false;
            packetFactory.encCfg.captureSalts = true;

            outPacket = new Packet(DataID.Hello, id);
            outPacket.Add((int)strength);
            SendPacket(outPacket);

            packetFactory.InitEncCfg(strength);
            packetFactory.encCfg.useCrpyto = false;
            packetFactory.encCfg.captureSalts = true;
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(packetFactory.encCfg.RSA_KEY_BITS))
            {
                packetFactory.encCfg.pub = rsa.ExportParameters(false);
                packetFactory.encCfg.priv = rsa.ExportParameters(true);
            }

            socket.BeginReceive(dataStream.New(), 0, HEADER_SIZE, SocketFlags.None, new AsyncCallback((IAsyncResult ar) => {
                receivingHeader = true;
                HandshakeRecursive(ar);
            }), dataStream);

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
        /// A method used to send Packets from the Client to the server
        /// </summary>
        /// <param name="packet">The packet to send</param>
        private void SendPacket(Packet packet)
        {
            byte[] data = packetFactory.GetDataStream(packet);
            if (socket.ProtocolType == ProtocolType.Tcp)
            {
                ManualResetEvent sent = new ManualResetEvent(false);
                socket.BeginSend(BitConverter.GetBytes(data.Length), 0, HEADER_SIZE, 0, new AsyncCallback((IAsyncResult ar) => {
                    socket.EndSend(ar);
                    sent.Set();
                }), null);
                sent.WaitOne();
                socket.BeginSend(data, 0, data.Length, 0, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);
            }
            else
                socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, server, new AsyncCallback((IAsyncResult ar) => { socket.EndSendTo(ar); }), null);
        }

        /// <summary>
        /// The callback to run when TCP packets are recieved from the server
        /// </summary>
        /// <param name="ar">An object representing the result of the asynchoronous task</param>
        private protected override void ReceiveTCPCallback(IAsyncResult ar, int bytesToRead = HEADER_SIZE)
        {
            try
            {
                DataStream dataStream = (DataStream)ar.AsyncState;
                int bytesRead = socket.EndReceive(ar);
                if (receivingHeader)
                {
                    bytesToRead = BitConverter.ToInt32(dataStream.Get()) + HEADER_SIZE; //(+ HEADER_SIZE because when we pass the recursive CB we subtract bytesRead from bytesToRead)
                    receivingHeader = false;
                }
                if (bytesToRead - bytesRead > 0)
                    socket.BeginReceive(dataStream.New(), 0, dataStream.bufferSize, SocketFlags.None, new AsyncCallback((IAsyncResult ar) => {
                        ReceiveTCPCallback(ar, bytesToRead - bytesRead);
                    }), dataStream);
                else
                {
                    receiving.Set();
                    inPackets.Add(packetFactory.BuildPacket(dataStream.Get()));
                }                
            }
            catch (SocketException)
            {
                Disconnect();
            }
            catch (ObjectDisposedException) { }
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
                    inPackets.Add(packetFactory.BuildPacket(dataStream.Get()));
                receiving.Set();
            }
            catch (SocketException)
            {
                Disconnect();
            }
            catch (ObjectDisposedException) { }            
        }

        /// <summary>
        /// A method to disconnect from the server when it dies.
        /// </summary>
        private void Disconnect()
        {
            socket.DisconnectAsync(null);
            Dispose();
            Console.WriteLine("Connection to server has died");
        }

        /// <summary>
        /// A method used to free resources used by the ClientChannel
        /// </summary>
        /// <param name="disposing">A boolean to represent the state of disposing</param>
        private protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cts.Cancel();
                    socket.Dispose();
                }

                disposedValue = true;
            }
        }

        #endregion
    }
}
