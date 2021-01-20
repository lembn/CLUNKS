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
    class ServerChannel : Channel
    {
        #region Private Members

        private List<ClientModel> clientList;

        #endregion

        #region Methods

        public ServerChannel(int bufferSize, IPAddress address) : base(bufferSize)
        {
            clientList = new List<ClientModel>();
            PacketFactory.SetValues();
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var ep = new IPEndPoint(address, TCP_PORT);
            socket.Bind(ep);
            socket.Listen(128);
        }

        public override void Start()
        {
            Console.WriteLine("Echoserver started");
            Console.WriteLine("Listening");

            threads.Add(ThreadHelper.GetECThread(ctoken, () => { 
                //Accept connections and add to clientlist
            }));
        }

        private protected override void Heartbeat()
        {
            throw new NotImplementedException();
        }

        private protected override void ReceiveTCPCallback(IAsyncResult ar)
        {
            throw new NotImplementedException();
        }

        private protected override void ReceiveUDPCallback(IAsyncResult ar)
        {
            throw new NotImplementedException();
        }

        private protected override void SendPacket(Packet packet)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cts.Cancel();
                    PacketFactory.Cleanup();
                    foreach (ClientModel client in clientList)
                    {
                        client.handler.Dispose();
                    }
                }

                disposedValue = true;
                dataStream = null;
            }
        }

        #endregion
    }
}
