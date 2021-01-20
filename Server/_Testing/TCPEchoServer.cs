using System;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

namespace Server._Testing
{
    class TCPEchoServer
    {
        #region Private Members

        private Socket socket;
        private byte[] dataStream = new byte[10000];
        private byte[] TCPHeaderBuffer = new byte[4];
        private int HEADER_SIZE = 4;
        private Socket clientHandler;
        private bool listening = false;
        private ManualResetEvent connected;

        #endregion

        #region Methods

        public TCPEchoServer()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var ep = new IPEndPoint(IPAddress.Loopback, 40000);
            socket.Bind(ep);
            socket.Listen(128);
            connected = new ManualResetEvent(false);
        }

        public void Start()
        {
            Console.WriteLine("Echoserver started");
            Console.WriteLine("Listening");

            socket.BeginAccept(new AsyncCallback((IAsyncResult ar) => {
                clientHandler = socket.EndAccept(ar);
                connected.Set();
                Console.WriteLine("Connection accepted");
            }), null);

            connected.WaitOne();

            var listen = Task.Run(() => {
                while (true)
                {
                    if (!listening)
                    {
                        listening = true;
                        clientHandler.BeginReceive(TCPHeaderBuffer, 0, HEADER_SIZE, SocketFlags.None, new AsyncCallback(ReceiveTCPCallback), null);
                    }
                    Thread.Sleep(10);
                }
            });

            listen.Wait();
        }

        private void ReceiveTCPCallback(IAsyncResult ar)
        {
            clientHandler.EndReceive(ar);
            ManualResetEvent received = new ManualResetEvent(false);
            int bytesToRead = BitConverter.ToInt32(TCPHeaderBuffer);
            clientHandler.BeginReceive(dataStream, 0, bytesToRead, SocketFlags.None, new AsyncCallback((IAsyncResult ar) => {
                clientHandler.EndReceive(ar);
                received.Set();
            }), null);

            received.WaitOne();

            ManualResetEvent sent = new ManualResetEvent(false);
            byte[] header = BitConverter.GetBytes(dataStream.Length);
            clientHandler.BeginSend(header, 0, header.Length, 0, new AsyncCallback((IAsyncResult ar) => {
                clientHandler.EndSend(ar);
                sent.Set();
            }), null);
            sent.WaitOne();
            clientHandler.BeginSend(dataStream, 0, dataStream.Length, 0, new AsyncCallback((IAsyncResult ar) => { clientHandler.EndSend(ar); }), null);

            dataStream = new byte[10000];
            TCPHeaderBuffer = new byte[HEADER_SIZE];
            listening = false;
        }

        #endregion
    }
}
