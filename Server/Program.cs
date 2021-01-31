using Common.Channels;
using Common.Packets;
using System;
using System.Net;

namespace Server
{
    class Program
    {
        public static ServerChannel server;

        static void Main(string[] args)
        {
            server = new ServerChannel(1024, IPAddress.Parse("192.168.0.21"));
            server.Dispatch += Printer;
            server.Start();
            Console.ReadLine();
            server.cts.Cancel();
        }

        public static void Printer(object sender, PacketEventArgs e)
        {
            Console.WriteLine($"Received: {e.Packet.dataID} [{e.Packet.body}] from {e.Client.endpoint}\n");
            Console.WriteLine("Forwarding to all clients...");
            foreach (ClientModel client in server.clientList)
            {
                Console.WriteLine($"Sending to {client.endpoint}...");
                server.Add(e.Packet, client);
            }
        }
    }
}