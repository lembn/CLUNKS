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
            var channel = new ClientChannel(1024, IPAddress.Loopback, 30000, id);
            channel.Dispatch += Printer;
            channel.Start();
            //channel.Add(new Packet(DataID.Command, id));            
            Console.ReadLine();
            channel.cts.Cancel();
        }

        public static void Printer(object sender, PacketEventArgs e)
        {
            Console.WriteLine($"Received: {e.Packet.dataID} {e.Packet.body}\n");
        }
    }
}
