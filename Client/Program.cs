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
        private static bool prompted = false;
        private static string promptHeader = null;

        static void Main(string[] args)
        {
            Title();
            bool state = true;
            channel = new ClientChannel(1024, IPAddress.Parse(args[0]), Convert.ToInt32(args[1]), Convert.ToInt32(args[2]), EncryptionConfig.Strength.Strong, ref state);
            quit = !state;
            channel.ChannelFail += FailHandler;
            if (!quit)
                channel.Start();
            waiter = new AutoResetEvent(false);
            Packet outPacket;            
            while (!quit)
            {
                if (!prompted)
                {
                    if (promptHeader != null)
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine(promptHeader);
                        Console.ResetColor();
                    }
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
                        channel.Dispatch += ConnectReponseHanlder; //cause????
                        channel.Add(outPacket);
                        Console.WriteLine($"Requesting CONNECT to '{input[1]}'...");
                        break;
                    case "cls":
                    case "clear":
                        Console.Clear();
                        prompted = false;
                        break;
                    case "quit":
                    case "exit":
                        channel.Close("Shutting down CLUNKS...");
                        break;
                    default:
                        Console.WriteLine("Try 'help' for more info.");
                        prompted = false;
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
                Console.WriteLine($"CONNECT completed with status '{values[0].ToUpper()}'.");
                promptHeader = $"[{values[1]}]";
                channel.Dispatch -= ConnectReponseHanlder;
                prompted = false;
            }
            else
            {
                Packet outPacket = new Packet(DataID.Command, channel.id);
                string s = ConsoleTools.HideInput("Enter password");
                outPacket.Add(Communication.CONNECT, values[0].Split(Communication.SEPARATOR)[0], s, values[0].Split(Communication.SEPARATOR)[1]);
                channel.Add(outPacket);
            }            
        }

        private static void Title()
        {
            void Output(string s)
            {
                Console.Write(new string(' ', (Console.WindowWidth - s.Length) / 2)); ;
                Console.WriteLine(s);
            }

            string[] lines = new string[] { " ██████╗██╗     ██╗   ██╗███╗   ██╗██╗  ██╗███████╗", "██╔════╝██║     ██║   ██║████╗  ██║██║ ██╔╝██╔════╝",
                                            "██║     ██║     ██║   ██║██╔██╗ ██║█████╔╝ ███████╗", "██║     ██║     ██║   ██║██║╚██╗██║██╔═██╗ ╚════██║",
                                            "╚██████╗███████╗╚██████╔╝██║ ╚████║██║  ██╗███████║", " ╚═════╝╚══════╝ ╚═════╝ ╚═╝  ╚═══╝╚═╝  ╚═╝╚══════╝" };
            string bar = $"||{new string('=', lines[0].Length + 10)}||";

            Output(bar);
            foreach (string line in lines)
            {                
                Output(line);
                Thread.Sleep(50);
            }
            Output(bar);
        }

        private static void ShowHelp()
        {
            //TODO: Populate
            Console.WriteLine("TODO");
        }

        public static void FailHandler(object sender, ChannelFailEventArgs e)
        {
            Console.WriteLine(e.Message);
            quit = true;
        }
    }
}
