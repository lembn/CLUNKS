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
        #region Private Members

        private static ClientChannel channel;
        private static bool quit = false;
        private static bool prompted = false;
        private static bool hide = false;
        private static int promptRow;
        private static string capturedInput;
        private static string username = null;
        private static Stack<string> traversalTrace;
        private static AutoResetEvent waiter;

        #endregion

        static void Main(string[] args)
        {
            Console.CancelKeyPress += Quit;
            Title();
            Feed.Feed.Initialise(3);
            waiter = new AutoResetEvent(false);
            bool state = true;
            channel = new ClientChannel(1024, IPAddress.Parse(args[0]), Convert.ToInt32(args[1]), Convert.ToInt32(args[2]), EncryptionConfig.Strength.Strong, ref state);
            quit = !state;
            channel.ChannelFail += FailHandler;
            if (!quit)
                channel.Start();
            channel.MessageDispatch += MessageHandler;
            traversalTrace = new Stack<string>();
            RunLoop();
            Thread.Sleep(2000);
        }

        private static void RunLoop()
        {
            while (!quit)
            {
                bool entered = false;
                string captured = String.Empty;
                ConsoleKeyInfo keyInfo;
                do
                {
                    while (!Console.KeyAvailable && !quit)
                    {
                        if (!prompted)
                            Prompt();
                        Thread.Sleep(0);
                    }
                    if (quit)
                        break;
                    keyInfo = Console.ReadKey(true);
                    switch (keyInfo.Key)
                    {
                        case ConsoleKey.Enter:
                            Console.SetCursorPosition(0, ++Console.CursorTop);
                            if (hide)
                            {
                                waiter.Set();
                                hide = false;
                                capturedInput = captured;
                            }
                            else
                                Process(captured);
                            entered = true;
                            break;
                        case ConsoleKey.Backspace:
                            captured = ManualBackspace(captured);
                            break;
                        case ConsoleKey.OemPlus:
                            if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control) && Feed.Feed.isAlive)
                                Feed.Feed.Scroll(true);
                            break;
                        case ConsoleKey.OemMinus:
                            if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control) && Feed.Feed.isAlive)
                                Feed.Feed.Scroll(false);
                            break;
                        case ConsoleKey.UpArrow:
                        case ConsoleKey.DownArrow:
                            break;
                        case ConsoleKey.LeftArrow:
                            if (!String.IsNullOrEmpty(captured))
                                Console.CursorLeft--;
                            break;
                        case ConsoleKey.RightArrow:
                            Console.CursorLeft++;
                            break;
                        default:
                            Console.Write(hide ? '*' : keyInfo.KeyChar);
                            captured += keyInfo.KeyChar;
                            break;
                    }
                } while (!entered);
            }
        }
        
        private static void Prompt()
        {
            if (username != null)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                if (traversalTrace.Count > 0)
                {
                    Console.Write($"'{username}'");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(" @ ");
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    if (traversalTrace.Count > 3)
                    {
                        string header = String.Empty;
                        int i = 0;
                        bool dash = false;
                        foreach (string entity in traversalTrace.Reverse())
                        {
                            if (String.IsNullOrEmpty(header))
                                header = $"[{entity}";
                            if (i++ > traversalTrace.Count - 3)
                            {
                                header += dash ? $"- {entity}" : $"{entity}";
                                if (!dash)
                                    dash = true;
                            }
                            else if (!header.Contains("..."))
                            {
                                header += " ... ";
                                dash = false;
                            }

                        }
                        Console.WriteLine($"{header}]");
                    }
                    else
                        Console.WriteLine($"[{String.Join(" - ", traversalTrace.Reverse())}]");
                }
                else
                    Console.WriteLine($"'{username}'");
            }
            Console.ResetColor();
            Console.Write("CLUNKS>>> ");
            prompted = true;
            promptRow = Console.CursorTop - 1;
        }

        private static void Process(string source)
        {
            bool state;
            Packet outPacket;
            string[] input = source.Split();
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
                            Console.WriteLine("You must log into an account before connecting to entities");
                            prompted = false;
                            break;
                        }
                        outPacket = new Packet(DataID.Command, channel.id);
                        if (traversalTrace.Contains(input[1]))
                            outPacket.Add(input[0], Communication.START, input[1], username, Communication.BACKWARD, String.Join(" - ", traversalTrace));
                        else
                            outPacket.Add(input[0], Communication.START, input[1], username, Communication.FORWARD, traversalTrace.Count == 0 ? String.Empty : traversalTrace.Peek());
                        channel.StatusDispatch += ConnectReponseHanlder;
                        channel.Add(outPacket);
                        Console.WriteLine($"Requesting CONNECT to '{input[1]}'...");
                        break;
                    case Communication.LOGIN:
                        outPacket = new Packet(DataID.Command, channel.id);
                        outPacket.Add(input[0], Communication.START, input[1]);
                        channel.StatusDispatch += LoginResponseHandler;
                        channel.Add(outPacket);
                        break;
                    case Communication.MAKE_GROUP:
                        if (traversalTrace.Count == 0)
                        {
                            Console.WriteLine("You are not connected to any entities");
                            prompted = false;
                            break;
                        }
                        if (traversalTrace.Count == 1)
                        {
                            Console.WriteLine("You can't create groups directly on a subserver");
                            prompted = false;
                            break;
                        }
                        outPacket = new Packet(DataID.Command, channel.id);
                        outPacket.Add(input[0], input[1], input.Length > 2 ? input[2] : String.Empty, traversalTrace.Peek());
                        channel.StatusDispatch += MGResponseHandler;
                        channel.Add(outPacket);
                        break;
                    case Communication.CHAT:
                        if (username == null)
                        {
                            Console.WriteLine("You must log into an account before sending chats");
                            prompted = false;
                            break;
                        }
                        if (!traversalTrace.Contains(input[1].Substring(1)))
                        {
                            Console.WriteLine("Invalid entity");
                            prompted = false;
                            break;
                        }
                        if (String.IsNullOrEmpty(input[2].Trim()))
                        {
                            Console.WriteLine("You did not enter a message");
                            prompted = false;
                            break;
                        }
                        state = input[1][0] == '@';
                        if (!state && (input[1] == username))
                        {
                            Console.WriteLine("You cannot message yourself");
                            prompted = false;
                            break;
                        }
                        if (state && traversalTrace.Count == 0)
                        {
                            Console.WriteLine("You need to be connected to an entity to send global chats");
                            prompted = false;
                            break;
                        }
                        for (int i = Console.CursorTop - 1; i >= promptRow; i--)
                        {
                            Console.SetCursorPosition(0, i);
                            Console.Write(new string(' ', Console.WindowWidth));
                        }
                        Console.CursorTop--;
                        outPacket = new Packet(DataID.Command, channel.id);
                        string message = String.Join(' ', input.Skip(2));
                        if (state)
                            outPacket.Add(input[0], Communication.TRUE, username, input[1].Substring(1), message);
                        else
                            outPacket.Add(input[0], Communication.FALSE, username, input[1], message);
                        channel.StatusDispatch += ChatHandler;
                        channel.Add(outPacket);
                        break;
                    case Communication.FEED:
                        if (username == null)
                            Console.WriteLine("You must log into an account to view chats");
                        else
                            Feed.Feed.Show();
                        prompted = false;
                        break;
                    case "cls":
                        if (Feed.Feed.isAlive)
                            Feed.Feed.Deactivate(true);
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
                            channel.StatusDispatch += DisconnectResponseHandler;
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

        private static void ConnectReponseHanlder(object sender, PacketEventArgs e)
        {
            string[] values = e.packet.Get();
            if (Communication.STATUSES.Contains(values[0]))
            {
                Console.WriteLine($"CONNECT completed with status '{values[0].ToUpper()}'.");
                if (values[0] != Communication.FAILURE)
                    traversalTrace = new Stack<string>(values[1].Split(" - "));
                channel.StatusDispatch -= ConnectReponseHanlder;
                prompted = false;  
            }
            else
            {
                Packet outPacket = new Packet(DataID.Command, channel.id);
                hide = true;
                Console.Write($"Enter '{values[0]}' password>>> ");
                waiter.WaitOne();
                outPacket.Add(Communication.CONNECT, capturedInput, username);
                channel.Add(outPacket);
            }
        }

        private static void DisconnectResponseHandler(object sender, PacketEventArgs e)
        {
            string[] values = e.packet.Get();
            Console.WriteLine($"DISCONNECT completed with status '{values[0].ToUpper()}'.");
            if (values[0] != Communication.FAILURE)
                traversalTrace.Pop();
            channel.StatusDispatch -= DisconnectResponseHandler;
            prompted = false;
        }

        private static void LoginResponseHandler(object sender, PacketEventArgs e)
        {
            string[] values = e.packet.Get();
            if (Communication.FINAL_STATUSES.Contains(values[0]))
            {
                Console.WriteLine($"LOGIN completed with status '{values[0].ToUpper()}'.");
                if (values[0] != Communication.FAILURE)
                {
                    username = values[1];
                    Feed.Feed.YOU = username;
                }
                channel.StatusDispatch -= LoginResponseHandler;
                prompted = false;
            }
            else
            {
                Packet outPacket = new Packet(DataID.Command, channel.id);
                hide = true;
                Console.Write($"Enter your password>>> ");
                waiter.WaitOne();
                outPacket.Add(Communication.LOGIN, capturedInput);
                channel.Add(outPacket);
            }
        }

        private static void ChatHandler(object sender, PacketEventArgs e)
        {
            Console.WriteLine(e.packet.Get()[0] == Communication.SUCCESS ? "CHAT sent" : "Failed to send");
            channel.StatusDispatch -= ChatHandler;
            prompted = false;
        }

        private static void MGResponseHandler(object sender, PacketEventArgs e)
        {
            Console.WriteLine($"MAKE GROUP completed with status '{e.packet.Get()[0].ToUpper()}'.");
            channel.StatusDispatch -= MGResponseHandler;
            prompted = false;
        }

        private static void MessageHandler(object sender, PacketEventArgs e)
        {
            string[] values = e.packet.Get();
            Feed.Feed.Add(values[1], values[2], values[0] == Communication.TRUE ? traversalTrace.Peek() : null);
        }
        
        public static void FailHandler(object sender, ChannelFailEventArgs e)
        {
            Console.WriteLine(e.Message);
            Quit(null, null);
        }

        private static void Quit(object sender, ConsoleCancelEventArgs e)
        {
            Feed.Feed.Cleanup();
            channel.Close("Shutting down CLUNKS...");
            quit = true;
            if (e != null)
                e.Cancel = true;
        }

        private static string ManualBackspace(string line)
        {
            if (!String.IsNullOrEmpty(line))
            {
                line = line.Substring(0, line.Length - 1);
                Console.CursorLeft--;
                Console.Write(' ');
                Console.CursorLeft--;
            }
            
            return line;
        }

        private static void Title()
        {
            void Output(string s)
            {
                Console.Write(new string(' ', (Console.WindowWidth - s.Length) / 2));
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
            Console.WriteLine("TODO");
        }
    }
}
