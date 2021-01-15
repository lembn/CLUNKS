using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Common
{
    public class ClientChannel : Channel
    {
        #region Private Memebers

        private int userID;
        private bool receivedHB = false; //Received Heatbeat
        private int missedHBs = 0; //Missed Hearbeats
        private bool listening = false;
        private ManualResetEvent initial;
        private bool waited = false;

        #endregion

        #region Methods

        public ClientChannel(int bufferSize, IPAddress address, int port, int userID) : base(bufferSize, address, port)
        {
            this.userID = userID;
            initial = new ManualResetEvent(false);
        }        

        public override void Start()
        {
            threads.Add(new Thread(() =>
            {
                while (true && !ctoken.IsCancellationRequested)
                {
                    Heartbeat();
                }
            }));

            threads.Add(new Thread(() =>
            {
                while (true && !ctoken.IsCancellationRequested)
                {
                    Packet packet;
                    bool packetAvailable;
                    lock (outPackets) { packetAvailable = outPackets.TryDequeue(out packet); }
                    if (packetAvailable)
                    {
                        SendData(packet);
                    }
                }
            }));

            threads.Add(new Thread(() =>
            {
                while (true && !ctoken.IsCancellationRequested)
                {                    
                    if (!waited)
                    {
                        initial.WaitOne();                        
                    }                    
                    if (!listening)
                    {
                        listening = true;
                        _ = socket.BeginReceiveFrom(dataStream, 0, dataStream.Length, SocketFlags.None, ref endpoint, new AsyncCallback(ReceiveData), null);
                    }                    
                }
            }));

            threads.Add(new Thread(() =>
            {
                while (true && !ctoken.IsCancellationRequested)
                {
                    byte[] packetBytes;
                    bool packetAvailable;
                    lock (inPackets) { packetAvailable = inPackets.TryDequeue(out packetBytes); }
                    if (packetAvailable)
                    {
                        var inPacket = new Packet(packetBytes);
                        if (inPacket.dataID == DataID.Heartbeat)
                        {
                            OnDispatch(inPacket);
                            lock (hbLock) { receivedHB = true; }
                        }
                        else
                        {
                            OnDispatch(inPacket);
                        }
                    }
                }
            }));

            foreach (var thread in threads)
            {
                thread.Start();
            }
        }

        protected override void Heartbeat()
        {
            var heartbeat = new Packet(DataID.Heartbeat, userID);
            lock (outPackets) { outPackets.Enqueue(heartbeat); }            
            Thread.Sleep(5000);
            lock (hbLock)
            {
                if (receivedHB)
                {
                    receivedHB = false;
                    missedHBs = 0;
                }
                else
                {
                    missedHBs += 1;
                    if (missedHBs == 2)
                    {
                        Dispose();
                        //TODO: alert owner of ClientChannel that connection has died
                        Console.WriteLine("Connection to server has died");
                    }
                }
            }
        }

        protected override void SendData(Packet packet)
        {
            byte[] byteData = packet.GetDataStream();

            socket.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None, endpoint, new AsyncCallback((IAsyncResult ar) => { socket.EndSendTo(ar); }), null);

            if (!waited)
            {
                initial.Set();
                waited = true;
            }
        }

        protected override void ReceiveData(IAsyncResult ar)
        {
            _ = socket.EndReceiveFrom(ar, ref endpoint);
            lock (inPackets) { inPackets.Enqueue((byte[])dataStream.Clone()); }
            dataStream = new byte[bufferSize];
            listening = false;
        }

        #endregion
    }
}
