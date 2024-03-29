﻿using Common.Packets;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
        private protected bool disposed; //A boolean to represent if the channel has been closed or not

        #endregion

        #region Public Members

        public delegate void DispatchEventHandler(object sender, PacketEventArgs e);
        public delegate void ChannelFailEventHanlder(object sender, ChannelFailEventArgs e); //A delegate to represent the event handler used for hadling the ChannelFail event
        public event ChannelFailEventHanlder ChannelFail; //An event to represent when something has gone wrong in the channel
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

        ~Channel() => Dispose(false);

        public abstract void Start();
        private protected abstract void ReceiveUDPCallback(IAsyncResult ar);
        private protected abstract void ReceiveTCPCallback(IAsyncResult ar, int bytesToRead);

        /// <summary>
        /// A method for alerting Channel owners of problems occuring within the channel
        /// </summary>
        /// <param name="message">A fail message</param>
        public virtual void OnChannelFail(string message)
        {
            if (ChannelFail != null)
                Task.Run(() => { ChannelFail(this, new ChannelFailEventArgs(message)); });
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
