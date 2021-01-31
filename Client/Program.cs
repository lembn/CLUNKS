using System;
using System.Net;
using Common.Channels;
using Common.Helpers;
using Common.Packets;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            var channel = new ClientChannel(1024, IPAddress.Parse("192.168.0.21"), EncryptionConfig.Strength.Strong);
            channel.Dispatch += Printer;
            channel.Start();
            channel.Add(new Packet(DataID.AV, channel.id));
            Console.ReadLine();
            channel.cts.Cancel();
        }

        public static void Printer(object sender, PacketEventArgs e)
        {
            Console.WriteLine($"Received: {e.Packet.dataID} {e.Packet.body}\n");
        }
    }
}
