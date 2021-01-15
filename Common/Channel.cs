using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Common
{
    public abstract class Channel : IDisposable
    {
        #region Private Members

        private protected Queue<byte[]> inPackets;
        private protected Queue<Packet> outPackets;
        private protected List<Thread> threads;
        private protected byte[] dataStream;
        private protected int bufferSize;
        private protected Socket socket;
        private protected EndPoint endpoint;
        private protected CancellationToken ctoken;
        private protected object hbLock; //Heartbeat lock
        private bool disposedValue;
        
        #endregion

        #region Public Members

        public delegate void DispatchEventHandler(object sender, PacketEventArgs e);
        public event DispatchEventHandler Dispatch;
        public CancellationTokenSource cts;       
        
        #endregion

        #region Methods

        protected Channel(int bufferSize, IPAddress address, int port)
        {
            this.bufferSize = bufferSize;
            dataStream = new byte[bufferSize];
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            endpoint = new IPEndPoint(address, port);
            //socket.Bind(endpoint);
            inPackets = new Queue<byte[]>();
            outPackets = new Queue<Packet>();
            threads = new List<Thread>();
            cts = new CancellationTokenSource();
            ctoken = cts.Token;
            hbLock = new object();
        }

        public abstract void Start();
        protected abstract void Heartbeat();

        protected abstract void ReceiveData(IAsyncResult ar);
        protected abstract void SendData(Packet packet);

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
}
