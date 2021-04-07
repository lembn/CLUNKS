using Common.Helpers;
using Common.Packets;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Common.Channels
{
    /// <summary>
    /// A class used by the ServerChannel to model the attributes of a client
    /// </summary>
    public class ClientModel : DataStream, IDisposable
    {
        private Socket handler;        
        public bool disposed;
        
        #region Public Members

        public uint id = Channel.NULL_ID;
        public EndPoint endpoint;
        public PacketFactory packetFactory;
        public ProtocolType protocol;
        public bool receiving = false;
        public bool receivingHeader = true;
        public bool receivedHB;
        public int missedHBs;
        public bool isAdmin = false;
        public Dictionary<string, object> data;

        public Socket Handler
        {
            get { return handler; }
            set 
            {
                handler = value;
                endpoint = value.RemoteEndPoint;
            }
        }

        #endregion

        public ClientModel()
        {
            packetFactory = new PacketFactory();
            packetFactory.InitEncCfg(EncryptionConfig.Strength.Strong);
            packetFactory.encCfg.useCrypto = false;
            packetFactory.encCfg.captureSalts = true;
            protocol = ProtocolType.Tcp;
            data = new Dictionary<string, object>();
        }

        #region IDisposable Implementation

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    handler.Dispose();
                    packetFactory.Dispose();
                }

                Array.Clear(buffer, 0, buffer.Length);
                data.Clear();
                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
