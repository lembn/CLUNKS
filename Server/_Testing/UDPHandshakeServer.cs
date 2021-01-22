using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Common.Helpers;
using Common.Packets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Server._Testing
{
    class UDPHandshakeServer
    {
        #region Private Memebers

        private Socket socket;
        private byte[] dataStream = new byte[10000];
        private JObject body;
        private byte[] data;
        private Packet packet;
        private int counter = 0;
        private PacketFactory packetFactory;

        #endregion

        #region Methods

        public UDPHandshakeServer()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var server = new IPEndPoint(IPAddress.Loopback, 30000);
            socket.Bind(server);
        }

        public void Start()
        {
            Console.WriteLine("Handshake server started");

            EndPoint senderEP = new IPEndPoint(IPAddress.Any, 0);

            Console.WriteLine("Listening");

            packetFactory = new PacketFactory();
            packetFactory.InitEncCfg(EncryptionConfig.Strength.Strong);
            packetFactory.encCfg.useCrpyto = false;
            packetFactory.encCfg.captureSalts = true;

            socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref senderEP, new AsyncCallback(ReceiveData), null);
            var task = Task.Run(() => { while (true) { Console.ReadLine(); } });
            task.Wait();
        }

        private void ReceiveData(IAsyncResult asyncResult)
        {            
            packet = packetFactory.BuildPacket(dataStream);

            EndPoint senderEP = new IPEndPoint(IPAddress.Any, 0);

            socket.EndReceiveFrom(asyncResult, ref senderEP);            

            switch (packet.dataID)
            {
                case DataID.Hello:
                    string strengthString = packet.body.GetValue(Packet.BODYFIRST).ToString();
                    packetFactory.InitEncCfg((EncryptionConfig.Strength)BitConverter.ToInt32(Convert.FromBase64String(strengthString)));
                    packetFactory.encCfg.useCrpyto = false;
                    packetFactory.encCfg.captureSalts = true;
                    packet = new Packet(DataID.Ack, 1);
                    data = packetFactory.GetDataStream(packet);
                    socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, senderEP, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);
                    break;
                case DataID.Info:
                    string clientKey = packet.body.GetValue(Packet.BODYFIRST).ToString();
                    using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(packetFactory.encCfg.RSA_KEY_BITS))
                    {
                        packetFactory.encCfg.recipient = JsonConvert.DeserializeObject<RSAParameters>(clientKey);
                        packetFactory.encCfg.priv = rsa.ExportParameters(true);
                        packetFactory.encCfg.pub = rsa.ExportParameters(false);
                    }
                    packet = new Packet(DataID.Hello, 2);
                    packet.Add(ObjectConverter.GetJObject(packetFactory.encCfg.pub));
                    data = packetFactory.GetDataStream(packet);
                    socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, senderEP, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);
                    packetFactory.encCfg.useCrpyto = true;
                    break;
                case DataID.Ack:
                    packet = new Packet(DataID.Info, 2);
                    packet.Add(0);
                    data = packetFactory.GetDataStream(packet);
                    socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, senderEP, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);
                    packetFactory.encCfg.captureSalts = false;
                    break;
                case DataID.Signature:
                    string clientSignatureStr = packet.body.GetValue(Packet.BODYFIRST).ToString();
                    byte[] clientSignature = Convert.FromBase64String(clientSignatureStr);
                    byte[] signature;
                    string signatureStr;
                    using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                    {
                        rsa.ImportParameters(packetFactory.encCfg.recipient);
                        if (!rsa.VerifyData(packetFactory.incomingSalts.ToArray(), SHA512.Create(), clientSignature))
                            signatureStr = "failure";
                        else
                        {
                            rsa.ImportParameters(packetFactory.encCfg.priv);
                            signature = rsa.SignData(packetFactory.outgoingSalts.ToArray(), SHA512.Create());
                            signatureStr = Convert.ToBase64String(signature);
                        }
                    }
                    packet = new Packet(DataID.Signature, 2);
                    packet.Add(signatureStr);
                    data = packetFactory.GetDataStream(packet);
                    socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, senderEP, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);
                    break;
            }

            socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref senderEP, new AsyncCallback(ReceiveData), null);
        }

        #endregion
    }
}
