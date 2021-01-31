using Common.Packets;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Common.Channels
{
    /// <summary>
    /// The base class for all Channel implementations.
    /// </summary>
    public abstract class Channel
    {
        #region Private Members

        private protected const int HEADER_SIZE = 4; //bodyLength is a 32 bit integer  
        private protected int bufferSize; //The default buffer size of the channel
        private protected CancellationToken ctoken; //A token used for cancelling threads when the channel is closed
        private protected List<Thread> threads; //A list to keep track of running threads        
        private protected const string SUCCESS = "success"; //A constant value used for identifying a sucessfull operation
        private protected const string FAILURE = "failure"; //A constant value used for identifying a failed operation
        private protected const int UDP_PORT = 30000; //Port used by the server for UDP
        private protected const int TCP_PORT = 40000; //Port used by the server for TCP
        private protected bool disposedValue; //A boolean to represent if the channel has been closed or not

        #endregion

        #region Public Members

        public delegate void DispatchEventHandler(object sender, PacketEventArgs e); //A delegate to represent the event handler used for handling the Dispatch event
        public event DispatchEventHandler Dispatch; //An event to represent a packet that should be processed by the owner of a channel
        public CancellationTokenSource cts; //An object used to obtain Cancellation Tokens (when cancelling threaded operations)
        public const int NULL_ID = 1; //User ID for users who haven't been assigned ID yet
        
        #endregion

        #region Methods

        /// <summary>
        /// The base constructor for all Channel implementations/
        /// </summary>
        /// <param name="bufferSize">The size to allocate to the buffer of the channel (in bytes)</param>
        /// <param name="address">The IP address of the server</param>
        /// <param name="port">The port that the server is hosting on</param>
        private protected Channel(int bufferSize)
        {
            threads = new List<Thread>();
            cts = new CancellationTokenSource();
            ctoken = cts.Token;
            this.bufferSize = bufferSize;
        }

        public abstract void Start();
        private protected abstract void ReceiveUDPCallback(IAsyncResult ar);
        private protected abstract void ReceiveTCPCallback(IAsyncResult ar, int bytesToRead);

        /// <summary>
        /// A method for releasing packets to the owner of the channel
        /// </summary>
        /// <param name="packet">The packet to dispatch</param>
        public virtual void OnDispatch(Packet packet)
        {
            if (Dispatch != null)
                Dispatch(this, new PacketEventArgs() { Packet = packet });
        }
        public virtual void OnDispatch((Packet, ClientModel) data)
        {
            if (Dispatch != null)
                Dispatch(this, new PacketEventArgs() 
                { 
                    Packet = data.Item1,
                    Client = data.Item2
                });
        }

        private protected abstract void Dispose(bool disposing);
        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
