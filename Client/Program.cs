using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
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
        private static bool pass = true;
        private static bool prompted = false;
        private static string promptHeader = null;
        private static string username = null;
        private static Stack<string> traversalTrace;

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
            traversalTrace = new Stack<string>();
            while (!quit)
            {
                if (!prompted)
                {
                    if (username != null)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        if (promptHeader != null)
                        {
                            Console.Write($"'{username}'");
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.Write(" @ ");
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine(promptHeader);
                        }
                        else
                            Console.WriteLine($"'{username}'");
                    }                    
                    Console.ResetColor();
                    Console.Write("CLUNKS>>> ");
                    prompted = true;
                }
                if (!Console.KeyAvailable || !pass)
                {
                    Thread.Sleep(10);
                    continue;
                }
                pass = true;
                string[] input = Console.ReadLine().Split();
                Array.ForEach(input, x => x = x.Trim());
                try
                {
                    switch (input[0].ToLower())
                    {
                        case "help":
                            ShowHelp();
                            break;
                        case Communication.CONNECT:
                            if (!(traversalTrace.Count == 0))
                                if (input[1] == traversalTrace.Peek())
                                {
                                    Console.WriteLine($"Already connected to {input[1]}");
                                    prompted = false;
                                    break;
                                }
                            if (username == null)
                            {
                                Console.WriteLine("You must log into a subserver before connecting to entities");
                                prompted = false;
                                break;
                            }
                            outPacket = new Packet(DataID.Command, channel.id);
                            if (traversalTrace.Contains(input[1]))
                                outPacket.Add(input[0], Communication.START, input[1], username, Communication.BACKWARD, String.Join(" - ", traversalTrace));
                            else
                                outPacket.Add(input[0], Communication.START, input[1], username, Communication.FORWARD, traversalTrace.Count == 0 ? String.Empty : traversalTrace.Peek());
                            channel.Dispatch += new Channel.DispatchEventHandler(ConnectReponseHanlder);
                            channel.Add(outPacket);
                            Console.WriteLine($"Requesting CONNECT to '{input[1]}'...");
                            break;
                        case Communication.LOGIN:
                            outPacket = new Packet(DataID.Command, channel.id);
                            outPacket.Add(input[0], Communication.START, input[1]);
                            channel.Dispatch += new Channel.DispatchEventHandler(LoginResponseHandler);
                            channel.Add(outPacket);
                            break;
                        case "trace":
                            Console.WriteLine(String.Join(" - ", traversalTrace));
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
                    traversalTrace = new Stack<string>(values[1].Split(" - "));
                    promptHeader = $"[{values[1]}]";
                }
                channel.Dispatch -= ConnectReponseHanlder;
                prompted = false;
                pass = true;             
            }
            else
            {
                Packet outPacket = new Packet(DataID.Command, channel.id);
                pass = false;
                outPacket.Add(Communication.CONNECT, ConsoleTools.HideInput($"Enter '{values[0]}' password"), username);
                channel.Add(outPacket);
            }
        }

        private static void DisconnectResponseHandler(object sender, PacketEventArgs e)
        {
            string[] values = e.packet.Get();
            Console.WriteLine($"DISCONNECT completed with status '{values[0].ToUpper()}'.");
            if (values[0] != Communication.FAILURE)
            {
                traversalTrace.Pop();
                promptHeader = traversalTrace.Count > 0 ? $"[{string.Join(" - ", traversalTrace.Reverse())}]" : null;
            }
            channel.Dispatch -= DisconnectResponseHandler;
            prompted = false;
        }

        private static void LoginResponseHandler(object sender, PacketEventArgs e)
        {
            string[] values = e.packet.Get();
            if (Communication.FINAL_STATUSES.Contains(values[0]))
            {
                Console.WriteLine($"LOGIN completed with status '{values[0].ToUpper()}'.");
                if (values[0] != Communication.FAILURE)
                    username = values[1];
                channel.Dispatch -= LoginResponseHandler;
                prompted = false;
                pass = true;
            }
            else
            {
                Packet outPacket = new Packet(DataID.Command, channel.id);
                pass = false;
                outPacket.Add(Communication.LOGIN, ConsoleTools.HideInput($"Enter your password"));
                channel.Add(outPacket);
            }
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
