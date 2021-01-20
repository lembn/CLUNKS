using Common.Helpers;
using System.Net.Sockets;

namespace Common.Channels
{
    public class ClientModel
    {
        public uint id;
        public Socket handler;
        public EncryptionConfig encCfg;
        public ProtocolType protocol;
    }
}
