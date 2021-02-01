using Common.Channels;
using Common.Packets;
using Common.Helpers;
using System;
using System.Net;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using System.Collections.Generic;

namespace Server
{
    class Program
    {
        static ServerChannel server;
        static bool loggedIn = false;

        static void Main(string[] args)
        {
            string cfgLoc = String.Concat(Assembly.GetEntryAssembly().Location, ".config");
            if (!File.Exists(cfgLoc))
            {
                Console.WriteLine("CLUNKS>>> Configuration file could not be found!");
                bool initialise = ConsoleTools.AskYesNo("CLUNKS>>> Would you like to initialse the config file.");
                if (!initialise)
                    return;
                InitialiseConfig(cfgLoc);
            }

            while (true)
            {
                Console.Write("CLUNKS>>> ");
                args = Console.ReadLine().Split();
                List<string> similar = new List<string> { "changeuser", "ipaddress", "tcpport", "udpport", "datapath" };
                if (args.Length == 2)
                {
                    if (similar.Contains(args[0].ToLower()))
                        IfLoggedIn(() => { ModifyConfig(args[0], args[1]); });
                    else
                    {
                        switch (args[0].ToLower())
                        {
                            case "login":
                                Login();
                                break;
                            case "changepwd":
                                IfLoggedIn(() => { ModifyConfig("password", BCrypt.Net.BCrypt.HashPassword(args[1])); });
                                break;
                            case "start":
                                IfLoggedIn(() => { StartServer(Convert.ToInt32(args[1])); });
                                break;
                            case "stop":
                                IfLoggedIn(StopServer);
                                break;
                            case "help":
                                ShowHelp();
                                break;
                            default:
                                Console.WriteLine("CLUNKS>>> Try 'help' for more information.");
                                break;
                        }
                    }
                }                
            }
        }

        private static void Login()
        {
            Console.Write("CLUNKS>>> Enter Admin username: ");
            if (Console.ReadLine() == ConfigurationManager.AppSettings.Get("username"))
            {
                Console.Write("CLUNKS>>> Enter Admin password: ");
                loggedIn = BCrypt.Net.BCrypt.Verify(ConsoleTools.HideInput(), ConfigurationManager.AppSettings.Get("password"));
                if (!loggedIn)
                    ShowError("Incorrect password.");
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("CLUNKS>>> Welocome!");
                    Console.ResetColor();
                }
            }
            else
                ShowError("Incorrect username.");
        }

        private static void StartServer(int bufferSize)
        {
            if (loggedIn)
            {
                server = new ServerChannel(bufferSize, IPAddress.Parse(ConfigurationManager.AppSettings.Get("ipaddress")));
                server.Dispatch += DispatchHandler;
                server.Start();
                //TODO: [Server.Program] when the server starts it should always run even if the program is closed
            }
            else
                ShowError("You must be logged in to start the server.");
        }

        private static void StopServer()
        {
            if (loggedIn)
                server.Dispose();
            else
                ShowError("You must be logged in to start the server.");
        }

        private static void DispatchHandler(object sender, PacketEventArgs e)
        {
            Console.WriteLine($"Received: {e.Packet.dataID} [{e.Packet.body}] from {e.Client.endpoint}\n");
            Console.WriteLine("Forwarding to all clients...");
            foreach (ClientModel client in server.clientList)
            {
                Console.WriteLine($"Sending to {client.endpoint}...");
                server.Add(e.Packet, client);
            }
        }
        
        private static void InitialiseConfig(string location)
        {
            string dataPath = String.Concat(Directory.GetCurrentDirectory(), @"\data");
            if (!Directory.Exists(dataPath))
                Directory.CreateDirectory(dataPath);

            XElement configuration = new XElement("configuration",
                new XElement("appSettings", 
                    new XElement("add", new XAttribute("key", "username"), new XAttribute("value", "admin")),
                    new XElement("add", new XAttribute("key", "password"), new XAttribute("value", BCrypt.Net.BCrypt.HashPassword("Clunks77"))),
                    new XElement("add", new XAttribute("key", "ipaddress"), new XAttribute("key", "127.0.0.1")),
                    new XElement("add", new XAttribute("key", "tcpPort"), new XAttribute("value", "40000")),
                    new XElement("add", new XAttribute("key", "udpPort"), new XAttribute("value", "30000")),
                    new XElement("add", new XAttribute("key", "dataPath"), new XAttribute("key", dataPath))));

            configuration.Save(location);
        }

        private static void ModifyConfig(string key, string replacement)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings[key].Value = replacement;
            config.Save(ConfigurationSaveMode.Modified);
        }

        private static void IfLoggedIn(Action task)
        {
            if (!loggedIn)
                ShowError("You must be logged in to change the administrator's password.");
            else
                task();
        }

        private static void ShowError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"CLUNKS>>> {message}");
            Console.ResetColor();
        }

        private static void ShowHelp()
        {
            Console.WriteLine("'login'\nLogin to the program with the admin credentials.");
        }

    }
}