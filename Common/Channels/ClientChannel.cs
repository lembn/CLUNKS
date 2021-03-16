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
        private ManualResetEventSlim receiving; //An waithandle used to check if the channel is currently listening on the socket
        private bool receivingHeader = true; //A boolean to represent if a TCP listen is currently reading the header or the actual packet
        private object hbLock; //A lock used for thread synchronisation when processing heartbeats
        private bool receivedHB = false; //A boolean used for checking if the channel has received a hearbeat or not
        private int missedHBs = 0; //A counter of how many hearbeats have been missed
        private Socket socket; //The socket to listen on (and send over)
        private EndPoint server; //Represents the endpoint of the server
        private IPAddress serverIP; //Server IP
        private readonly int TCP_PORT; //Port used by the server for TCP communication
        private readonly int UDP_PORT; //Port used by the server for UDP communication
        private int connectAttempts = 3; //The maximum amount of handshakes to attempt before aborting
        private ManualResetEvent connected; //An waithandle to represent if a connection has been made to the server when using TCP
        private ManualResetEvent complete; //An waithandle to represent if a handshake attempt has completed
        private int largePackets = 0; //Number of datagrams that were too large for the buffer size
        private int packetCount; //Number of packets received in total
        private double packetLossThresh = 0.05; //Threshold to alert user about significant packet loss
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
        public ClientChannel(int bufferSize, IPAddress ip, int tcp, int udp, EncryptionConfig.Strength strength, ref bool valid) : base(bufferSize)
        {
            Console.WriteLine("Opening communications...");
            outPackets = new BlockingCollection<Packet>();
            inPackets = new BlockingCollection<Packet>();
            receiving = new ManualResetEventSlim(true);
            connected = new ManualResetEvent(false);
            complete = new ManualResetEvent(false);
            this.strength = strength;
            packetFactory = new PacketFactory();
            hbLock = new object();
            serverIP = ip;
            TCP_PORT = tcp;
            UDP_PORT = udp;
            dataStream = new DataStream(bufferSize);
            server = (EndPoint)new IPEndPoint(serverIP, TCP_PORT);
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            bool state = true;
            socket.BeginConnect(server, new AsyncCallback((IAsyncResult ar) => 
            {
                try
                {
                    socket.EndConnect(ar);
                }
                catch (Exception)
                {
                    Console.WriteLine($"There is no CLUNK server at {ip}:{TCP_PORT}");
                    state = false;
                }
                connected.Set();
            }), null);
            connected.WaitOne();
            valid = state;
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
                ctoken.WaitHandle.WaitOne(5000);
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
                            Close();
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
                try
                {
                    receiving.Wait(ctoken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                receiving.Reset();
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
                ctoken.WaitHandle.WaitOne(30000);
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
                        lock (hbLock)
                            receivedHB = true;
                    else
                        OnDispatch(packet);
                }
            })); //Dispatch

            int attempts = 0;
            while (true)
            {                
                try
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
                        Close($"Failed to connect {attempts} times, aborting");
                        break;
                    }
                    Console.WriteLine("Re-attempting...");
                }
                catch (SocketException)
                {
                    Close();
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
        /// A method to disconnect from the server when it dies.
        /// </summary>
        public void Close(string message = "Connection to server has died.")
        {
            if (disposed)
                return;
            if (socket.ProtocolType == ProtocolType.Tcp)
                socket?.Disconnect(false);
            complete.Set();
            cts.Cancel();
            Dispose();
            OnChannelFail($"------------------\n{message}\nQuitting...");
        }

        /// <summary>
        /// A method to perform a handshake with the server
        /// </summary>
        /// <returns>true if the handshake was successfull of false otherwise</returns>
        private bool Handshake()
        {
            Packet outPacket = null;
            byte[] signature;
            bool failed = false;
            List<DataID> expectedDataList = new List<DataID> { DataID.Ack, DataID.Hello, DataID.Info, DataID.Signature };
            Queue<DataID> expectedData = new Queue<DataID>(expectedDataList);

            void HandshakeRecursive(IAsyncResult ar, int bytesToRead = 0)
            {
                try
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
                        ProcessPacket(packetFactory.BuildPacket(dataStream.Get()));
                }
                catch (SocketException)
                {
                    Close();
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
                            string serverParamString = inPacket.Get()[0];
                            packetFactory.encCfg.recipient = JsonConvert.DeserializeObject<RSAParameters>(serverParamString);
                            packetFactory.encCfg.useCrypto = true;
                            outPacket = new Packet(DataID.Ack, id);
                            break;
                        case DataID.Info:
                            id = Convert.ToUInt32(inPacket.Get()[0]);
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
                            string sigStr = inPacket.Get()[0];
                            if (sigStr == Communication.FAILURE)
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
                                    outPacket.Add(Communication.SUCCESS);
                                else
                                {
                                    failed = true;
                                    outPacket.Add(Communication.FAILURE);
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

            Console.WriteLine($"Handshaking with server @{server}");

            packetFactory.InitEncCfg(EncryptionConfig.Strength.Strong);
            packetFactory.encCfg.useCrypto = false;
            packetFactory.encCfg.captureSalts = true;

            outPacket = new Packet(DataID.Hello, id);
            outPacket.Add((int)strength);
            SendPacket(outPacket);

            packetFactory.InitEncCfg(strength);
            packetFactory.encCfg.useCrypto = false;
            packetFactory.encCfg.captureSalts = true;
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(packetFactory.encCfg.RSA_KEY_BITS))
            {
                packetFactory.encCfg.pub = rsa.ExportParameters(false);
                packetFactory.encCfg.priv = rsa.ExportParameters(true);
            }

            try
            {
                socket.BeginReceive(dataStream.New(), 0, HEADER_SIZE, SocketFlags.None, new AsyncCallback((IAsyncResult ar) => {
                    receivingHeader = true;
                    HandshakeRecursive(ar);
                }), dataStream);
            }
            catch (ObjectDisposedException) { }
                

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
                try
                {
                    socket.BeginSend(BitConverter.GetBytes(data.Length), 0, HEADER_SIZE, 0, new AsyncCallback((IAsyncResult ar) =>
                    {
                        socket.EndSend(ar);
                        sent.Set();
                    }), null);
                    sent.WaitOne();
                    socket.BeginSend(data, 0, data.Length, 0, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);
                }
                catch (SocketException)
                {
                    Close();
                }
                catch (ObjectDisposedException) { }
            }
            else
            {
                try
                {
                    socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, server, new AsyncCallback((IAsyncResult ar) => { socket.EndSendTo(ar); }), null);
                }
                catch (SocketException)
                {
                    Close();
                }
                catch (ObjectDisposedException) { }
            }
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
                {
                    socket.BeginReceive(dataStream.New(), 0, dataStream.bufferSize, SocketFlags.None, new AsyncCallback((IAsyncResult ar) => {
                        ReceiveTCPCallback(ar, bytesToRead - bytesRead);
                    }), dataStream);
                }                    
                else
                {
                    inPackets.Add(packetFactory.BuildPacket(dataStream.Get()));
                    receiving.Set();
                }                
            }
            catch (SocketException)
            {
                Close();
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
                Close();
            }
            catch (ObjectDisposedException) { }            
        }

        /// <summary>
        /// A method used to free resources used by the ClientChannel
        /// </summary>
        /// <param name="disposing">A boolean to represent the state of disposing</param>
        private protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    cts.Cancel();
                    socket.Dispose();
                }

                disposed = true;
            }
        }

        #endregion
    }
}
