using System;
using System.Net;
using System.Net.Sockets;

namespace Common
{
    public abstract class Channel : IDisposable
    {
        #region Private Members

        private protected byte[] dataStream;
        private protected int bufferSize;
        private protected Socket socket;
        private protected EndPoint endpoint;
        private bool disposedValue;

        #endregion

        #region Methods

        protected Channel(int bufferSize, IPAddress address, int port)
        {
            this.bufferSize = bufferSize;
            dataStream = new byte[bufferSize];
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            endpoint = new IPEndPoint(address, port);
        }

        protected abstract void Start();

        protected abstract void ReceiveData(IAsyncResult ar);

        protected abstract void SendData(Packet packet);

        protected abstract void Dispatch(Packet packet);

        protected abstract void SendHeartbeat();
        

        #region IDisposable implementation

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
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
