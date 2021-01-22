using System;
using System.Net.Sockets;
using System.Net;
using Common.Packets;
using System.Threading.Tasks;

namespace Server._Testing
{
    public class UDPEchoServer
    {
        #region Private Members

        private Socket socket;
        private byte[] dataStream = new byte[10000];
        private PacketFactory packetFactory;

        #endregion

        #region Methods

        public UDPEchoServer()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var server = new IPEndPoint(IPAddress.Loopback, 30000);
            socket.Bind(server);
            packetFactory = new PacketFactory();
        }

        public void Start()
        {
            Console.WriteLine("Echoserver started");
       
            EndPoint senderEP = new IPEndPoint(IPAddress.Any, 0);

            Console.WriteLine("Listening");

            socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref senderEP, new AsyncCallback(ReceiveData), null);
            var task = Task.Run(() => { while (true) { Console.ReadLine(); } });
            task.Wait();
        }

        private void ReceiveData(IAsyncResult asyncResult)
        {
            var packet = packetFactory.BuildPacket(dataStream);
            packet.body.Add("server", true);

            EndPoint senderEP = new IPEndPoint(IPAddress.Any, 0);

            socket.EndReceiveFrom(asyncResult, ref senderEP);

            byte[] data = packetFactory.GetDataStream(packet);

            socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, senderEP, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);
            Console.WriteLine($"Echoing: {senderEP}");

            socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref senderEP, new AsyncCallback(ReceiveData), null);
        }

        #endregion
    }
}