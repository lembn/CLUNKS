using Common.Channels;
using Common.Helpers;
using Common.Packets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    //TODO: Add logging
    //TODO: Write summaries
    internal class Worker : BackgroundService
    {
        private readonly ILogger<Worker> logger;
        private static ServerChannel server;

        internal Worker(ILogger<Worker> logger) => this.logger = logger;

        public override Task StartAsync(CancellationToken stoppingToken)
        {
            string cfgLoc = String.Concat(Assembly.GetEntryAssembly().Location, ".config");
            string dataLoc = String.Concat(Directory.GetCurrentDirectory(), @"\data");
            string accessLoc = String.Concat(Directory.GetCurrentDirectory(), @"\access");
            if (!File.Exists(cfgLoc))
                ConfigHandler.InitialiseConfig(cfgLoc);
            if (!Directory.Exists(dataLoc))
            {
                Directory.CreateDirectory(dataLoc);
                ConfigHandler.ModifyConfig("dataPath", dataLoc);
            }
            if (!Directory.Exists(accessLoc))
                Directory.CreateDirectory(accessLoc);
            if (ConfigurationManager.AppSettings.Get("newExp") == "true")
                DBHandler.LoadExp(Directory.GetFiles(accessLoc, "*.exp")[0], dataLoc);
            Start();
            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            server.Dispose();
            return Task.CompletedTask;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;

        private static void Start()
        {
            int bufferSize = Convert.ToInt32(ConfigurationManager.AppSettings.Get("buffferSize"));
            IPAddress ip = IPAddress.Parse(ConfigurationManager.AppSettings.Get("ipaddress"));
            int tcp = Convert.ToInt32(ConfigurationManager.AppSettings.Get("tcpPort"));
            int udp = Convert.ToInt32(ConfigurationManager.AppSettings.Get("udpPort"));
            server = new ServerChannel(bufferSize, ip, tcp, udp);
            server.Dispatch += DispatchHandler;
            //TODO: Check to see if a new exp needs to be loaded by checking App.config.newExp
            server.Start();
        }

        private static void DispatchHandler(object sender, PacketEventArgs e)
        {
            Packet outPacket = null;
            switch (e.Packet.dataID)
            {
                case DataID.Login:
                    string username = e.Packet.body.GetValue(String.Format(Packet.DATA, 0)).ToString();
                    string password = e.Packet.body.GetValue(String.Format(Packet.DATA, 1)).ToString();
                    if (username == ConfigurationManager.AppSettings.Get("username"))
                        e.Client.isAdmin = BCrypt.Net.BCrypt.Verify(password, ConfigurationManager.AppSettings.Get("password"));
                    else
                        e.Client.isAdmin = false;
                    outPacket = new Packet(DataID.Status, e.Client.id);
                    outPacket.Add(e.Client.isAdmin ? Communication.SUCCESS : Communication.FAILURE);
                    server.Add(outPacket, e.Client);
                    break;
                case DataID.Command:
                    string command = e.Packet.body.GetValue(Packet.BODYFIRST).ToString();
                    if (e.Packet.body.Count > 1)
                    {
                        outPacket = new Packet(DataID.Status, e.Client.id);
                        outPacket.Add(e.Client.isAdmin ? Communication.SUCCESS : Communication.FAILURE);
                        if (e.Client.isAdmin)
                        {
                            if (Communication.ADMIN_CONFIG.Contains(command))
                            {
                                string key = e.Packet.body.GetValue(String.Format(Packet.DATA, 0)).ToString();
                                string value = e.Packet.body.GetValue(String.Format(Packet.DATA, 1)).ToString();
                                ConfigHandler.ModifyConfig(key, value);
                            }
                            else if (command == Communication.PASSWORD)
                            {
                                string key = e.Packet.body.GetValue(String.Format(Packet.DATA, 0)).ToString();
                                string value = e.Packet.body.GetValue(String.Format(Packet.DATA, 1)).ToString();
                                ConfigHandler.ModifyConfig(key, BCrypt.Net.BCrypt.HashPassword(value));
                            }
                        }
                    }
                    else
                    {
                        if (Communication.ADMIN_COMMANDS.Contains(command) && !e.Client.isAdmin)
                        {
                            outPacket = new Packet(DataID.Status, e.Client.id);
                            outPacket.Add(Communication.FAILURE);
                        }
                        else if (Communication.INFO_COMMANDS.Contains(command))
                        {
                            outPacket = new Packet(DataID.Info, e.Client.id);
                            outPacket.Add(ConfigurationManager.AppSettings.Get(command));
                        }
                        else
                            switch (command)
                            {
                                //TODO handle other commands here
                                case Communication.RESTART:
                                    server.Dispose();
                                    server.Start();
                                    break;
                                case Communication.STOP:
                                    server.Dispose();
                                    outPacket = new Packet(DataID.Status, e.Client.id);
                                    outPacket.Add(Communication.SUCCESS);
                                    break;
                            }
                    }
                    server.Add(outPacket, e.Client);
                    break;
                case DataID.AV:
                    //TODO: use DB to figure out which users need to be sent the AV packet
                    break;
            }
        }
    }
}
