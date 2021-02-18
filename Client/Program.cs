using System;
using System.Linq;
using System.Net;
using System.Threading;
using Common.Channels;
using Common.Helpers;
using Common.Packets;

namespace Client
{
    class Program
    {
        private static ClientChannel channel;
        private static AutoResetEvent waiter;
        private static int tableIndex = 0;

        static void Main(string[] args)
        {
            channel = new ClientChannel(1024, IPAddress.Parse(args[0]), Convert.ToInt32(args[1]), Convert.ToInt32(args[2]), EncryptionConfig.Strength.Strong);
            if (!channel.stable)
            {
                Console.WriteLine("Quitting...");
                Thread.Sleep(2000);
                return;
            }
            channel.ChannelFail += FailHandler;
            channel.Start();
            Packet outPacket;
            bool quit = false;
            while (!quit)
            {
                Console.Write("CLUNKS>>> ");
                string[] input = Console.ReadLine().Split();
                switch (input[0].ToLower())
                {
                    case "help":
                        ShowHelp();
                        break;
                    case "connect":
                        outPacket = new Packet(DataID.Command, channel.id);
                        outPacket.Add(input[0], Communication.START, input[1], input[2]);
                        channel.Add(outPacket);
                        Console.WriteLine($"Requesting CONNECT to {input[1]}...");
                        channel.Dispatch += ConnectReponseHanlder;
                        waiter.WaitOne();
                        break;
                    case "quit":
                        quit = true;
                        break;
                    default:
                        Console.WriteLine("Try 'help' for more info.");
                        break;
                }                
            }
            channel.cts.Cancel();
        }

        private static void ConnectReponseHanlder(object sender, PacketEventArgs e)
        {
            //TODO: if req is accepted make user present in link table
            string[] values = e.Packet.body.Values<string>().ToArray();
            if (Communication.STATUSES.Contains(values[0]))
            {
                Console.WriteLine($"CONNECT completed with status '{values[0]}'");
                tableIndex += values[0] == Communication.SUCCESS ? 1 : 0;
                channel.Dispatch -= ConnectReponseHanlder;
                waiter.Set();
            }
            else
            {
                Packet outPacket = new Packet(DataID.Command, channel.id);
                outPacket.Add(Communication.CONNECT, values[0], ConsoleTools.HideInput("Enter password"));
                channel.Add(outPacket);
            }            
        }

        private static void ShowHelp()
        {
            throw new NotImplementedException();
        }

        public static void FailHandler(object sender, ChannelFailEventArgs e)
        {
            Console.WriteLine(e.Message);
            //TODO: If fixable, fix error and set channel.stable to true, otherwise quit
            channel.stable = true;
        }
    }
}
