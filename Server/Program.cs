using Common.Channels;
using System.Net;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new ServerChannel(10000, IPAddress.Loopback);
            server.Start();
        }
    }
}