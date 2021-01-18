﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Common
{
    /// <summary>
    /// The base class for all Channel implementations.
    /// </summary>
    public abstract class Channel : IDisposable
    {
        #region Private Members

        private protected Queue<byte[]> inPackets; //A queue to hold incoming packets
        private protected Queue<Packet> outPackets; //A queue to hold outgoing packets
        private protected List<Thread> threads; //A list to keep track of running threads
        private protected byte[] dataStream; //The buffer used for receiving messages
        private protected int bufferSize; //The size of the receiving buffer
        private protected Socket socket; //The socket to listen on and send over
        private protected EndPoint endpoint; //A representation of the server in the form of an IP address and port
        private protected CancellationToken ctoken; //A token used for cancelling threads when the channel is closed
        private protected object hbLock; //A lock used for thread synchronisation when processing heartbeats
        private protected const string SUCCESS = "success"; //A constant value used for identifying a sucessfull operation
        private protected const string FAILURE = "failure"; //A constant value used for identifying a failed operation
        private bool disposedValue; //A boolean to represent if the channel has been closed or not
        
        #endregion

        #region Public Members

        public delegate void DispatchEventHandler(object sender, PacketEventArgs e); //A delegate to represent the event handler used for handling the Dispatch event
        public event DispatchEventHandler Dispatch; //An event to represent a packet that should be processed by the owner of a channel
        public CancellationTokenSource cts; //An object used to obtain Cancellation Tokens (when cancelling threaded operations)
        
        #endregion

        #region Methods

        /// <summary>
        /// The base constructor for all Channel implementations/
        /// </summary>
        /// <param name="bufferSize">The size to allocate to the buffer of the channel (in bytes)</param>
        /// <param name="address">The IP address of the server</param>
        /// <param name="port">The port that the server is hosting on</param>
        private protected Channel(int bufferSize, IPAddress address, int port)
        {
            this.bufferSize = bufferSize;
            dataStream = new byte[bufferSize];
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            endpoint = new IPEndPoint(address, port);
            inPackets = new Queue<byte[]>();
            outPackets = new Queue<Packet>();
            threads = new List<Thread>();
            cts = new CancellationTokenSource();
            ctoken = cts.Token;
            hbLock = new object();
        }

        public abstract void Start();
        private protected abstract void Heartbeat();

        private protected abstract void ReceiveData(IAsyncResult ar);
        private protected abstract void SendData(Packet packet);

        /// <summary>
        /// A method to expose the outPackets queue so that members outside the Channel class can
        /// add packets to be sent.
        /// </summary>
        /// <param name="packet">The packet to be sent</param>
        public virtual void Add(Packet packet) => outPackets.Enqueue(packet);

        public virtual void OnDispatch(Packet packet)
        {
            if (Dispatch != null)
            {
                Dispatch(this, new PacketEventArgs() { Packet = packet });
            }
        }

        #region IDisposable implementation

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cts.Cancel();
                    Packet.Cleanup();
                    socket.Dispose();
                }

                disposedValue = true;
                dataStream = null;
            }
        }

        ~Channel()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #endregion
    }

    /// <summary>
    /// A class used to represent a problem during a handshake
    /// </summary>
    public class HandshakeException : Exception { }
}
