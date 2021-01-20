using Server._Testing;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new TCPEchoServer();
            server.Start();
        }
    }
}