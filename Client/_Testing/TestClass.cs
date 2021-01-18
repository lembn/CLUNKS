using System;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using Common.Packets;

namespace Client._Testing
{
    public class TestClass
    {
        #region Private Members

        private Socket socket;
        private uint id;
        private EndPoint serverEP;
        private byte[] dataStream = new byte[1024];

        #endregion

        #region Methods

        public TestClass(uint userID)
        {
            id = userID;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverEP = new IPEndPoint(IPAddress.Loopback, 30000);
        }

        public void Send()
        {
            Packet outPacket = new Packet(PacketFactory.DataID.Heartbeat, id, null);
            outPacket.body.Add("test", 909);

            Console.WriteLine($"Sending: {outPacket.body}");

            byte[] byteData = PacketFactory.GetDataStream(outPacket);

            socket.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None, serverEP, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);

            socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref serverEP, new AsyncCallback(ReceiveData), null);
            var task = Task.Run(() => { while (true) { Console.ReadLine(); } });
            task.Wait();
        }

        private void ReceiveData(IAsyncResult ar)
        {
            socket.EndReceive(ar);

            var inPacket = PacketFactory.BuildPacket(dataStream);

            Console.WriteLine($"Got: {inPacket.body}");

            dataStream = new byte[1024];

            socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref serverEP, new AsyncCallback(ReceiveData), null);
        }

        #endregion
    }
}