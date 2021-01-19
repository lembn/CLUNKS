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
    class HandshakeEchoServer
    {
        #region Private Memebers

        private Socket socket;
        private byte[] dataStream = new byte[10000];
        private JObject body;
        private byte[] data;
        private Packet packet;
        private bool handshaking = true;

        #endregion

        #region Methods

        public HandshakeEchoServer()
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

            PacketFactory.SetValues();
            PacketFactory.InitEncCfg(EncryptionConfig.Strength.Strong);
            PacketFactory.encCfg.useCrpyto = false;
            PacketFactory.encCfg.captureSalts = true;

            socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref senderEP, new AsyncCallback(ReceiveData), null);
            var task = Task.Run(() => { while (true) { Console.ReadLine(); } });
            task.Wait();
        }

        private void ReceiveData(IAsyncResult asyncResult)
        {
            packet = PacketFactory.BuildPacket(dataStream);

            EndPoint senderEP = new IPEndPoint(IPAddress.Any, 0);

            socket.EndReceiveFrom(asyncResult, ref senderEP);

            if (handshaking)
            {
                switch (packet.dataID)
                {
                    case PacketFactory.DataID.Hello:
                        string strengthString = packet.body.GetValue(PacketFactory.bodyToString[PacketFactory.BodyTag.Strength]).ToString();
                        PacketFactory.InitEncCfg((EncryptionConfig.Strength)BitConverter.ToInt32(Convert.FromBase64String(strengthString)));
                        PacketFactory.encCfg.useCrpyto = false;
                        PacketFactory.encCfg.captureSalts = true;
                        packet = new Packet(PacketFactory.DataID.Ack, 1, new JObject());
                        data = PacketFactory.GetDataStream(packet);
                        socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, senderEP, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);
                        break;
                    case PacketFactory.DataID.Info:
                        string clientKey = packet.body.GetValue(PacketFactory.bodyToString[PacketFactory.BodyTag.Key]).ToString();
                        using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(PacketFactory.encCfg.RSA_KEY_BITS))
                        {
                            PacketFactory.encCfg.recipient = JsonConvert.DeserializeObject<RSAParameters>(clientKey);
                            PacketFactory.encCfg.priv = rsa.ExportParameters(true);
                            PacketFactory.encCfg.pub = rsa.ExportParameters(false);
                        }
                        body = new JObject();
                        body.Add(PacketFactory.bodyToString[PacketFactory.BodyTag.Key], ObjectConverter.GetJObject(PacketFactory.encCfg.pub));
                        packet = new Packet(PacketFactory.DataID.Hello, 2, body);
                        data = PacketFactory.GetDataStream(packet);
                        socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, senderEP, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);
                        PacketFactory.encCfg.useCrpyto = true;
                        break;
                    case PacketFactory.DataID.Ack:
                        body = new JObject();
                        body.Add(PacketFactory.bodyToString[PacketFactory.BodyTag.ID], 0);
                        packet = new Packet(PacketFactory.DataID.Info, 2, body);
                        data = PacketFactory.GetDataStream(packet);
                        socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, senderEP, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);
                        PacketFactory.encCfg.captureSalts = false;
                        break;
                    case PacketFactory.DataID.Signature:
                        string clientSignatureStr = packet.body.GetValue(PacketFactory.bodyToString[PacketFactory.BodyTag.Signature]).ToString();
                        byte[] clientSignature = Convert.FromBase64String(clientSignatureStr);
                        byte[] signature;
                        string signatureStr;
                        using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                        {
                            rsa.ImportParameters(PacketFactory.encCfg.recipient);
                            if (!rsa.VerifyData(PacketFactory.incomingSalts.ToArray(), SHA512.Create(), clientSignature))
                                signatureStr = "failure";
                            else
                            {
                                rsa.ImportParameters(PacketFactory.encCfg.priv);
                                signature = rsa.SignData(PacketFactory.outgoingSalts.ToArray(), SHA512.Create());
                                signatureStr = Convert.ToBase64String(signature);
                            }
                        }
                        body = new JObject();
                        body.Add(PacketFactory.bodyToString[PacketFactory.BodyTag.Signature], signatureStr);
                        packet = new Packet(PacketFactory.DataID.Signature, 2, body);
                        data = PacketFactory.GetDataStream(packet);
                        socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, senderEP, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);
                        handshaking = false;
                        break;
                    default:
                        break;
                }
            }
            else
            {
                byte[] data = PacketFactory.GetDataStream(packet);
                socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, senderEP, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);
                Console.WriteLine($"Echoing: {senderEP}");
            }

            socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref senderEP, new AsyncCallback(ReceiveData), null);
        }

        #endregion
    }
}
