using System;
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
        private static bool quit = false;

        static void Main(string[] args)
        {
            bool state = true;
            channel = new ClientChannel(1024, IPAddress.Parse(args[0]), Convert.ToInt32(args[1]), Convert.ToInt32(args[2]), EncryptionConfig.Strength.Strong, ref state);
            quit = !state;
            channel.ChannelFail += FailHandler;
            if (!quit)
                channel.Start();
            waiter = new AutoResetEvent(false);
            Packet outPacket;
            bool prompted = false;
            while (!quit)
            {
                if (!prompted)
                {
                    Console.Write("CLUNKS>>> ");
                    prompted = true;
                }
                if (!Console.KeyAvailable)
                {
                    Thread.Sleep(10);
                    continue;
                }
                string[] input = Console.ReadLine().Split();
                switch (input[0].ToLower())
                {
                    case "help":
                        ShowHelp();
                        break;
                    case "connect":
                        outPacket = new Packet(DataID.Command, channel.id);
                        outPacket.Add(input[0], Communication.START, input[1], input[2]);
                        channel.Dispatch += ConnectReponseHanlder;
                        channel.Add(outPacket);
                        Console.WriteLine($"Requesting CONNECT to '{input[1]}'...");                        
                        waiter.WaitOne();
                        break;
                    case "quit":
                        channel.Close("Quitting...");
                        break;
                    default:
                        Console.WriteLine("Try 'help' for more info.");
                        break;
                }                
            }
            Thread.Sleep(2000);
        }

        private static void ConnectReponseHanlder(object sender, PacketEventArgs e)
        {
            string[] values = e.Packet.Get();
            if (Communication.STATUSES.Contains(values[0]))
            {
                Console.WriteLine($"CONNECT completed with status '{values[0]}'");
                channel.Dispatch -= ConnectReponseHanlder;
                waiter.Set();
            }
            else
            {
                Packet outPacket = new Packet(DataID.Command, channel.id);
                outPacket.Add(Communication.CONNECT, values[0].Split(Communication.SEPARATOR)[0], ConsoleTools.HideInput("Enter password"), values[0].Split(Communication.SEPARATOR)[1]);
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
            quit = true;
        }
    }
}
