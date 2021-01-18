using System;
using System.Net;
using Common;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            int id = 0;
            var channel = new ClientChannel(10000, IPAddress.Loopback, 30000, id);
            channel.Dispatch += Printer;
            channel.Start();       
            Console.ReadLine();
            channel.cts.Cancel();
        }

        public static void Printer(object sender, PacketEventArgs e)
        {
            Console.WriteLine($"Received: {e.Packet.dataID} {e.Packet.body}\n");
        }
    }
}
