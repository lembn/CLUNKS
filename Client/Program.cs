using System;
using System.Collections.Generic;
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
        private static bool quit = false;
        private static bool free = true;
        private static bool prompted = false;
        private static string promptHeader = null;
        private static string username;
        private static Queue<string> traversalTrace;

        static void Main(string[] args)
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(Quit);
            Title();
            bool state = true;
            channel = new ClientChannel(1024, IPAddress.Parse(args[0]), Convert.ToInt32(args[1]), Convert.ToInt32(args[2]), EncryptionConfig.Strength.Strong, ref state);
            quit = !state;
            channel.ChannelFail += FailHandler;
            if (!quit)
                channel.Start();
            Packet outPacket;
            traversalTrace = new Queue<string>();
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
                if (!Console.KeyAvailable || !free)
                {
                    Thread.Sleep(10);
                    continue;
                }
                free = true;
                string[] input = Console.ReadLine().Split();
                try
                {
                    switch (input[0].ToLower())
                    {
                        case "help":
                            ShowHelp();
                            break;
                        case "connect":
                            outPacket = new Packet(DataID.Command, channel.id);
                            username = input[2];
                            outPacket.Add(input[0], Communication.START, input[1], input[2]);
                            channel.Dispatch += new Channel.DispatchEventHandler(ConnectReponseHanlder);
                            channel.Add(outPacket);
                            Console.WriteLine($"Requesting CONNECT to '{input[1]}'...");
                            break;
                        case "cls":
                            Console.Clear();
                            prompted = false;
                            break;
                        case "exit":
                            if (traversalTrace.Count == 0)
                                Quit(null, null);
                            else
                            {
                                //TODO: Test ET
                                outPacket = new Packet(DataID.Command, channel.id);
                                outPacket.Add(Communication.DISCONNECT, traversalTrace.Peek(), username);
                                channel.Dispatch += new Channel.DispatchEventHandler(DisconnectResponseHandler);
                                channel.Add(outPacket);
                                Console.WriteLine($"Leaving...");
                            }
                            break;
                        default:
                            Console.WriteLine("Try 'help' for more info.");
                            prompted = false;
                            break;
                    }
                }                
                catch (IndexOutOfRangeException)
                {
                    Console.WriteLine("Missing parameters, try 'help' for more info.");
                    prompted = false;
                }
            }
            Thread.Sleep(2000);
        }

        private static void ConnectReponseHanlder(object sender, PacketEventArgs e)
        {
            string[] values = e.packet.Get();
            if (Communication.STATUSES.Contains(values[0]))
            {
                Console.WriteLine($"CONNECT completed with status '{values[0].ToUpper()}'.");
                if (values[0] != Communication.FAILURE)
                {
                    traversalTrace.Enqueue(values[1]);
                    promptHeader = traversalTrace.Count == 1 ? $"[{values[1]}]" : $"[{string.Join(" - ", traversalTrace)}]";
                }                    
                channel.Dispatch -= ConnectReponseHanlder;
                prompted = false;
                free = true;
            }
            else
            {
                Packet outPacket = new Packet(DataID.Command, channel.id);
                free = false;
                string s = ConsoleTools.HideInput("Enter password");
                outPacket.Add(Communication.CONNECT, values[0].Split(Communication.SEPARATOR)[0], s, values[0].Split(Communication.SEPARATOR)[1]);
                channel.Add(outPacket);
            }
        }

        private static void DisconnectResponseHandler(object sender, PacketEventArgs e)
        {
            string[] values = e.packet.Get();
            Console.WriteLine($"DISCONNECT completed with status '{values[0].ToUpper()}'.");
            if (values[0] != Communication.FAILURE)
            {
                traversalTrace.Dequeue();
                promptHeader = traversalTrace.Count > 0 ? $"[{string.Join(" - ", traversalTrace)}]" : null;
            }
            prompted = false;
        }

        private static void Quit(object sender, ConsoleCancelEventArgs e)
        {
            channel.Close("Shutting down CLUNKS...");
            quit = true;
            if (e != null)
                e.Cancel = true;
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
