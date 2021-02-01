﻿using Common.Helpers;
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
    public class ServerChannel : Channel, IDisposable
    {
        #region Private Members

        private uint currentUserID = 2;
        private BlockingCollection<(Packet, ClientModel)> inPackets; //A queue to hold incoming packets
        private BlockingCollection<(Packet, ClientModel)> outPackets; //A queue to hold outgoing packets
        private Socket TCPSocket;
        private Socket UDPSocket;

        #endregion
        
        public List<ClientModel> clientList;

        #region Methods

        /// <summary>
        /// Server constructor
        /// </summary>
        /// <param name="address">The IP address to bind to</param>
        public ServerChannel(int bufferSize, IPAddress address) : base(bufferSize)
        {
            clientList = new List<ClientModel>();
            inPackets = new BlockingCollection<(Packet, ClientModel)>();
            outPackets = new BlockingCollection<(Packet, ClientModel)>();
            TCPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            UDPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var tcpEP = new IPEndPoint(address, TCP_PORT);
            var udpEP = new IPEndPoint(address, UDP_PORT);
            TCPSocket.Bind(tcpEP);
            UDPSocket.Bind(udpEP);
            TCPSocket.Listen(128);
        }

        /// <summary>
        /// A method to start the threads of the ServerChannel
        /// </summary>
        public override void Start()
        {
            Console.WriteLine("ServerChannel started");
            Console.WriteLine("Listening");

            threads.Add(ThreadHelper.GetECThread(ctoken, () => {               
                TCPSocket.BeginAccept(new AsyncCallback((IAsyncResult ar) => {
                    ClientModel client = new ClientModel(bufferSize);
                    client.Handler = TCPSocket.EndAccept(ar);
                    if (Handshake(client))
                        lock (clientList)
                            clientList.Add(client);
                }), null);
            })); //Accept connections and add to clientList

            threads.Add(ThreadHelper.GetECThread(ctoken, () => 
            {
                foreach (ClientModel client in clientList)
                {
                    lock (outPackets)
                        outPackets.Add((new Packet(DataID.Heartbeat, client.id), client));
                }
                Thread.Sleep(5000);
                foreach (ClientModel client in clientList)
                {
                    lock (client.hbLock)
                    {
                        if (client.receivedHB)
                        {
                            client.receivedHB = false;
                            client.missedHBs = 0;
                        }
                        else
                        {
                            client.missedHBs += 1;
                            if (client.missedHBs == 2)
                            {
                                RemoveClient(client);
                            }
                        }
                    }
                }
            })); //Heartbeat

            threads.Add(ThreadHelper.GetECThread(ctoken, () => {
                lock (clientList)
                {
                    foreach (ClientModel client in clientList)
                    {
                        if (!client.receiving)
                        {
                            if (client.protocol == ProtocolType.Tcp)
                            {
                                client.receiving = true;
                                lock (client.Handler)
                                    client.Handler.BeginReceive(client.New(), 0, HEADER_SIZE, SocketFlags.None, new AsyncCallback((IAsyncResult ar) => {
                                        client.receivingHeader = true;
                                        ReceiveTCPCallback(ar);
                                    }), client);
                            }
                            else
                            {
                                UDPSocket.BeginReceiveFrom(client.New(), 0, bufferSize, SocketFlags.None, ref client.endpoint, new AsyncCallback(ReceiveUDPCallback), client);
                            }
                        }
                    }
                }
            })); //Receive (TCP/UDP)

            threads.Add(ThreadHelper.GetECThread(ctoken, () => { 
                Packet packet = null;
                ClientModel client = null;
                (Packet, ClientModel) output = (packet, client);
                bool packetAvailable;
                lock (outPackets)
                    packetAvailable = outPackets.TryTake(out output);
                if (packetAvailable)
                    SendPacket(output.Item1, output.Item2);
            })); //Send packets

            threads.Add(ThreadHelper.GetECThread(ctoken, () => 
            {
                Packet packet = null;
                ClientModel client = null;
                (Packet, ClientModel) output = (packet, client);
                bool packetAvailable;
                lock (inPackets)
                    packetAvailable = inPackets.TryTake(out output);
                if (packetAvailable)
                    if (output.Item1.dataID == DataID.Heartbeat)
                        lock (output.Item2.hbLock)
                            output.Item2.receivedHB = true;
                    else OnDispatch(output);
            })); //Dispatch

            foreach (var thread in threads)
                thread.Start();
        }

        /// <summary>
        /// A method to add packets for the ServerChannel to send
        /// </summary>
        /// <param name="packet">The packet to send</param>
        /// <param name="client">The recipient</param>
        public void Add(Packet packet, ClientModel client) => outPackets.Add((packet, client));

        /// <summary>
        /// A method to perform a handshake with a connecting client
        /// </summary>
        /// <param name="client">The connecting client</param>
        /// <returns>true if the handshake was successful, false otherwise</returns>
        private bool Handshake(ClientModel client)
        {
            Packet outPacket = null;
            byte[] signature;
            ManualResetEvent complete = new ManualResetEvent(false);
            bool failed = false;
            bool useCrypto = false;
            bool captureSalts = true;
            List<DataID> expectedDataList = new List<DataID> { DataID.Hello, DataID.Info, DataID.Ack, DataID.Signature, DataID.Status };
            Queue<DataID> expectedData = new Queue<DataID>(expectedDataList);

            void HandshakeRecursive(IAsyncResult ar, int bytesToRead = 0)
            {
                ClientModel client = (ClientModel)ar.AsyncState;
                int bytesRead = client.Handler.EndReceive(ar);
                if (client.receivingHeader)
                {
                    bytesToRead = BitConverter.ToInt32(client.Get()) + HEADER_SIZE; //(+ HEADER_SIZE because when we pass the recursive CB we subtract bytesRead from bytesToRead)
                    client.receivingHeader = false;
                }
                if (bytesToRead - bytesRead > 0)
                    client.Handler.BeginReceive(client.New(), 0, client.bufferSize, SocketFlags.None, new AsyncCallback((IAsyncResult ar) => {
                        HandshakeRecursive(ar, bytesToRead - bytesRead); 
                    }), client);
                else
                    ProcessPacket(client.packetFactory.BuildPacket(client.Get()));
            }

            void ProcessPacket(Packet inPacket)
            { 
                if (inPacket.dataID != expectedData.Dequeue())
                {
                    complete.Set();
                    failed = true;
                }                

                switch (inPacket.dataID)
                {
                    case DataID.Hello:
                        string strengthString = inPacket.body.GetValue(Packet.BODYFIRST).ToString();
                        client.packetFactory.InitEncCfg((EncryptionConfig.Strength)Convert.ToInt32(strengthString));
                        client.packetFactory.encCfg.useCrpyto = false;
                        client.packetFactory.encCfg.captureSalts = true;
                        outPacket = new Packet(DataID.Ack, client.id);
                        break;
                    case DataID.Info:
                        string clientKey = inPacket.body.GetValue(Packet.BODYFIRST).ToString();
                        using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(client.packetFactory.encCfg.RSA_KEY_BITS))
                        {
                            client.packetFactory.encCfg.recipient = JsonConvert.DeserializeObject<RSAParameters>(clientKey);
                            client.packetFactory.encCfg.priv = rsa.ExportParameters(true);
                            client.packetFactory.encCfg.pub = rsa.ExportParameters(false);
                        }
                        outPacket = new Packet(DataID.Hello, client.id);
                        outPacket.Add(ObjectConverter.GetJObject(client.packetFactory.encCfg.pub));
                        useCrypto = true;
                        break;
                    case DataID.Ack:
                        outPacket = new Packet(DataID.Info, client.id);
                        outPacket.Add(++currentUserID);
                        captureSalts = false;
                        break;
                    case DataID.Signature:
                        string clientSignatureStr = inPacket.body.GetValue(Packet.BODYFIRST).ToString();
                        byte[] clientSignature = Convert.FromBase64String(clientSignatureStr);
                        string signatureStr;
                        using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                        {
                            rsa.ImportParameters(client.packetFactory.encCfg.recipient);
                            if (!rsa.VerifyData(client.packetFactory.incomingSalts.ToArray(), SHA512.Create(), clientSignature))
                            {
                                signatureStr = FAILURE;
                                complete.Set();
                                failed = true;
                            }                                
                            else
                            {
                                rsa.ImportParameters(client.packetFactory.encCfg.priv);
                                signature = rsa.SignData(client.packetFactory.outgoingSalts.ToArray(), SHA512.Create());
                                signatureStr = Convert.ToBase64String(signature);
                            }
                        }
                        outPacket = new Packet(DataID.Signature, client.id);
                        outPacket.Add(signatureStr);
                        break;
                    case DataID.Status:
                        if (inPacket.body.GetValue(Packet.BODYFIRST).ToString() == FAILURE)
                            failed = true;
                        complete.Set();
                        break;
                }

                if (!complete.WaitOne(0))
                {
                    SendPacket(outPacket, client);
                    client.Handler.BeginReceive(client.New(), 0, HEADER_SIZE, SocketFlags.None, new AsyncCallback((IAsyncResult ar) => {
                        client.receivingHeader = true;
                        HandshakeRecursive(ar);
                    }), client);
                    if (useCrypto)
                        client.packetFactory.encCfg.useCrpyto = true;
                    if (!captureSalts)
                        client.packetFactory.encCfg.captureSalts = false;
                }
            }

            client.Handler.BeginReceive(client.New(), 0, HEADER_SIZE, SocketFlags.None, new AsyncCallback((IAsyncResult ar) => {
                client.receivingHeader = true;
                HandshakeRecursive(ar); 
            }), client);

            complete.WaitOne();
            if (failed)
                return false;
            else
                return true;
        }

        /// <summary>
        /// The asynchronous callback to call when a TCP packet is received on a socket
        /// </summary>
        /// <param name="ar">The asynchronus result holding client </param>
        /// <param name="bytesToRead">The number of bytes left to read</param>
        private protected override void ReceiveTCPCallback(IAsyncResult ar, int bytesToRead = 0)
        {
            ClientModel client = (ClientModel)ar.AsyncState;
            try
            {
                int bytesRead = client.Handler.EndReceive(ar);
                if (client.receivingHeader)
                {
                    bytesToRead = BitConverter.ToInt32(client.Get()) + HEADER_SIZE; //(+ HEADER_SIZE because when we pass the recursive CB we subtract bytesRead from bytesToRead)
                    client.receivingHeader = false;
                }
                if (bytesToRead - bytesRead > 0)
                    client.Handler.BeginReceive(client.New(), 0, client.bufferSize, SocketFlags.None, new AsyncCallback((IAsyncResult ar) => {
                        ReceiveTCPCallback(ar, bytesToRead - bytesRead);
                    }), client);
                else
                {
                    client.receiving = false;
                    inPackets.Add((client.packetFactory.BuildPacket(client.Get()), client));
                }                    
            }
            catch (SocketException)
            {
                RemoveClient(client);
            }
            catch (ObjectDisposedException) { }
        }
        /// <summary>
        /// The asynchronous callback to call when a UDP packet is received on a socket
        /// </summary>
        /// <param name="ar">The asynchronus result holding client </param>
        private protected override void ReceiveUDPCallback(IAsyncResult ar)
        {
            ClientModel client = (ClientModel)ar.AsyncState;
            try
            {
                UDPSocket.EndReceiveFrom(ar, ref client.endpoint);
                lock (inPackets)
                    inPackets.Add((client.packetFactory.BuildPacket(client.Get()), client));
                client.receiving = false;
            }
            catch (SocketException)
            {
                RemoveClient(client);
            }
            catch (ObjectDisposedException) { }
        }

        /// <summary>
        /// A method to send a packet to a client
        /// </summary>
        /// <param name="packet">The packet to send</param>
        /// <param name="client">The recipient</param>
        private void SendPacket(Packet packet, ClientModel client)
        {
            byte[] data = client.packetFactory.GetDataStream(packet);
            if (client.protocol == ProtocolType.Tcp)
            {
                ManualResetEvent sent = new ManualResetEvent(false);
                client.Handler.BeginSend(BitConverter.GetBytes(data.Length), 0, HEADER_SIZE, 0, new AsyncCallback((IAsyncResult ar) => { 
                    client.Handler.EndSend(ar);
                    sent.Set();
                }), null);
                sent.WaitOne();
                client.Handler.BeginSend(data, 0, data.Length, 0, new AsyncCallback((IAsyncResult ar) => { client.Handler.EndSend(ar); }), null);
            }
            else
                UDPSocket.BeginSendTo(data, 0, data.Length, SocketFlags.None, client.Handler.RemoteEndPoint, new AsyncCallback((IAsyncResult ar) => { UDPSocket.EndSendTo(ar); }), null);
        }

        /// <summary>
        /// A method to remove disconnected clients from the clientList
        /// </summary>
        /// <param name="client">The client to remove</param>
        private void RemoveClient(ClientModel client)
        {
            clientList.Remove(client);
            client.Dispose();
        }
        
        /// <summary>
        /// A method to free resources used by the ServerChannel on closure
        /// </summary>
        /// <param name="disposing">A boolean to represent the closing state of the ServerChannel</param>
        private protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cts.Cancel();
                    foreach (ClientModel client in clientList)
                    {
                        client.Handler.Dispose();
                    }
                }

                disposedValue = true;
            }
        }
        
        #endregion
    }
}