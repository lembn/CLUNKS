using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    class ClientChannel : Channel
    {
        #region Private Memebers

        private Queue<byte[]> inPackets;
        private Queue<Packet> outPackets;
        private int userID;

        #endregion

        #region Constructor

        public ClientChannel(int bufferSize, IPAddress address, int port, int userID) : base(bufferSize, address, port)
        {
            inPackets = new Queue<byte[]>();
            outPackets = new Queue<Packet>();
            this.userID = userID;
        }

        #endregion

        #region Methods

        //TODO: Add Cancellation token
        protected override void Start()
        {
            Task.Run(() => {
                while (true)
                {
                    //TODO: Add flag to make sure this is always only run once at a time, lock??
                    _ = socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref endpoint, new AsyncCallback(ReceiveData), null);             
                }
            });

            Task.Run(() => {
                while (true)
                {
                    byte[] packetBytes;
                    if (inPackets.TryDequeue(out packetBytes))
                    {
                        Dispatch(new Packet(packetBytes));
                    }
                }                
            });


            Task.Run(() => {
                while (true)
                {
                    foreach (var packet in outPackets)
                    {
                        SendData(packet);
                    }
                }                
            });

            Task.Run(() => {
                while (true)
                {
                    SendHeartbeat();
                }
            });

        }

        protected override void SendData(Packet packet)
        {
            Console.WriteLine($"Sending: {packet.Body}");

            byte[] byteData = packet.GetDataStream();

            socket.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None, endpoint, new AsyncCallback((IAsyncResult ar) => { socket.EndSend(ar); }), null);

            socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref endpoint, new AsyncCallback(ReceiveData), null);
            var task = Task.Run(() => { while (true) { Console.ReadLine(); } });
            task.Wait();
        }

        protected override void ReceiveData(IAsyncResult ar)
        {
            socket.EndReceive(ar);
            inPackets.Enqueue((byte[])dataStream.Clone());
            dataStream = new byte[bufferSize];
        }

        protected override void Dispatch(Packet packet)
        {
            throw new NotImplementedException();
        }

        protected override void SendHeartbeat()
        {
            var heartbeat = new Packet(DataID.Heartbeat, userID);
            outPackets.Enqueue(heartbeat);
            Task.Delay(5000);
        }

        #endregion
    }
}
