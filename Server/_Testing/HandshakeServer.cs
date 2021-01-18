using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Common;
using Common.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Server._Testing
{
    class HandshakeServer
    {
        #region Private Memebers

        private Socket socket;
        private byte[] dataStream = new byte[10000];
        private JObject body;
        private byte[] data;
        private Packet packet;
        private List<byte> outSaltList;
        private List<byte> inSaltList;
        private int counter = 0;

        #endregion

        #region Methods

        public HandshakeServer()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var server = new IPEndPoint(IPAddress.Loopback, 30000);
            inSaltList = new List<byte>();
            outSaltList = new List<byte>();
            socket.Bind(server);
        }

        public void Start()
        {
            Console.WriteLine("Handshake server started");

            EndPoint senderEP = new IPEndPoint(IPAddress.Any, 0);

            Console.WriteLine("Listening");

            Packet.SetValues();
            Packet.InitEncCfg(EncryptionConfig.Strength.Strong);

            socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref senderEP, new AsyncCallback(ReceiveData), null);
            var task = Task.Run(() => { while (true) { Console.ReadLine(); } });
            task.Wait();
        }

        private void ReceiveData(IAsyncResult asyncResult)
        {
            if (counter < 3)
            {
                packet = new Packet(dataStream, ref inSaltList);
                counter++;
            }
            else
                packet = new Packet(dataStream);

            EndPoint senderEP = new IPEndPoint(IPAddress.Any, 0);

            socket.EndReceiveFrom(asyncResult, ref senderEP);            

            switch (packet.dataID)
            {
                case Packet.DataID.Hello:
                    string strengthString = packet.body.GetValue(Packet.bodyToString[Packet.BodyTag.Strength]).ToString();
                    Packet.InitEncCfg((EncryptionConfig.Strength)BitConverter.ToInt32(Convert.FromBase64String(strengthString)));
                    packet = new Packet(Packet.DataID.Ack, 1, new JObject());
                    data = packet.GetDataStream(ref outSaltList);
                    socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, senderEP, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);
                    break;
                case Packet.DataID.Info:
                    string clientKey = packet.body.GetValue(Packet.bodyToString[Packet.BodyTag.Key]).ToString();
                    using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(Packet.encCfg.RSA_KEY_BITS))
                    {
                        Packet.encCfg.recipient = JsonConvert.DeserializeObject<RSAParameters>(clientKey);
                        Packet.encCfg.priv = rsa.ExportParameters(true);
                        Packet.encCfg.pub = rsa.ExportParameters(false);
                    }
                    body = new JObject();
                    body.Add(Packet.bodyToString[Packet.BodyTag.Key], ObjectConverter.GetJObject(Packet.encCfg.pub));
                    packet = new Packet(Packet.DataID.Hello, 2, body);
                    data = packet.GetDataStream(ref outSaltList);
                    socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, senderEP, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);
                    Packet.encCfg.useCrpyto = true;
                    break;
                case Packet.DataID.Ack:
                    body = new JObject();
                    body.Add(Packet.bodyToString[Packet.BodyTag.ID], 0);
                    packet = new Packet(Packet.DataID.Info, 2, body);
                    data = packet.GetDataStream(ref outSaltList);
                    socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, senderEP, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);
                    break;
                case Packet.DataID.Signature:
                    string clientSignatureStr = packet.body.GetValue(Packet.bodyToString[Packet.BodyTag.Signature]).ToString();
                    byte[] clientSignature = Convert.FromBase64String(clientSignatureStr);
                    byte[] signature;
                    string signatureStr;
                    using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                    {
                        rsa.ImportParameters(Packet.encCfg.recipient);
                        if (!rsa.VerifyData(inSaltList.ToArray(), SHA512.Create(), clientSignature))
                            signatureStr = "FAILURE";
                        else
                        {
                            rsa.ImportParameters(Packet.encCfg.priv);
                            signature = rsa.SignData(outSaltList.ToArray(), SHA512.Create());
                            signatureStr = Convert.ToBase64String(signature);
                        }
                    }
                    body = new JObject();
                    body.Add(Packet.bodyToString[Packet.BodyTag.Signature], signatureStr);
                    packet = new Packet(Packet.DataID.Signature, 2, body);
                    data = packet.GetDataStream();
                    socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, senderEP, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);
                    break;
                default:
                    break;
            }

            socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref senderEP, new AsyncCallback(ReceiveData), null);
        }

        #endregion
    }
}
