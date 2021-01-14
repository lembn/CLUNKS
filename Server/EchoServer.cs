using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using Common;
using System.Threading.Tasks;

namespace Server
{
    public class EchoServer
    {
        #region Private Members

        private Socket socket;
        private byte[] dataStream = new byte[1024];

        #endregion

        #region Methods

        public EchoServer()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var server = new IPEndPoint(IPAddress.Loopback, 30000);
            socket.Bind(server);
        }

        public void Start()
        {
            Console.WriteLine("Echoserver started");
       
            var clients = new IPEndPoint(IPAddress.Any, 0);
            var senderEP = (EndPoint)clients;

            Console.WriteLine("Listening");

            socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref senderEP, new AsyncCallback(ReceiveData), senderEP);
            var task = Task.Run(() => { while (true) { Console.ReadLine(); } });
            task.Wait();

        }

        private void ReceiveData(IAsyncResult asyncResult)
        {
            var inPacket = new Packet(dataStream);
            var outPacket = new Packet(inPacket.DataIdentifier, inPacket.UserIdentifier);
            outPacket.Body = inPacket.Body;

            var clients = new IPEndPoint(IPAddress.Any, 0);
            var senderEP = (EndPoint)clients;

            socket.EndReceiveFrom(asyncResult, ref senderEP);

            byte[] data = outPacket.GetDataStream();

            socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, senderEP, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), senderEP);
            Console.WriteLine($"Echo {senderEP}");

            socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref senderEP, new AsyncCallback(ReceiveData), senderEP);
        }

        #endregion
    }
}