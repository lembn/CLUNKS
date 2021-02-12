using Common.Channels;
using Common.Helpers;
using Common.Packets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    //TODO: Write summaries
    internal class Worker : BackgroundService
    {
        private static ILogger<Worker> logger;
        private static ServerChannel server;

        internal Worker(ILogger<Worker> _logger) => logger = _logger;

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
            DBHandler.DBHandler.connectionString = String.Format(ConfigurationManager.ConnectionStrings["default"].ConnectionString, ConfigurationManager.AppSettings.Get("dataPath"));
            if (!Directory.Exists(accessLoc))
                Directory.CreateDirectory(accessLoc);
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
            if (ConfigurationManager.AppSettings.Get("newExp") == "true")
                DBHandler.DBHandler.LoadExp(ConfigurationManager.AppSettings.Get("dataPath"));
            server.Start();
        }

        private static void DispatchHandler(object sender, PacketEventArgs e)
        {
            Packet outPacket = null;
            string[] values;
            bool state;
            switch (e.Packet.dataID)
            {
                case DataID.Login:
                    values = e.Packet.body.Values<string>().ToArray();
                    if (values[0] == ConfigurationManager.AppSettings.Get("username"))
                        e.Client.isAdmin = BCrypt.Net.BCrypt.Verify(values[1], ConfigurationManager.AppSettings.Get("password"));
                    else
                        e.Client.isAdmin = false;
                    outPacket = new Packet(DataID.Status, e.Client.id);
                    outPacket.Add(e.Client.isAdmin ? Communication.SUCCESS : Communication.FAILURE);
                    server.Add(outPacket, e.Client);
                    logger.LogInformation($"'{e.Client.endpoint}' logged in as admin.");
                    break;
                case DataID.Command:
                    values = e.Packet.body.Values<string>().ToArray();
                    if (values.Length > 1)
                    {
                        outPacket = new Packet(DataID.Status, e.Client.id);
                        outPacket.Add(e.Client.isAdmin ? Communication.SUCCESS : Communication.FAILURE);
                        if (e.Client.isAdmin)
                        {
                            if (Communication.ADMIN_CONFIG.Contains(values[0]))
                            {
                                logger.LogInformation($"Admin@{e.Client.endpoint} changed '{values[1]}' from '{ConfigurationManager.AppSettings.Get(values[1])}' to '{values[2]}' in config.");
                                ConfigHandler.ModifyConfig(values[1], values[2]);
                            }
                            else if (values[0] == Communication.PASSWORD)
                            {
                                ConfigHandler.ModifyConfig(values[1], BCrypt.Net.BCrypt.HashPassword(values[2]));
                                logger.LogInformation($"Admin@{e.Client.endpoint} changed password.");
                            }
                        }
                    }
                    else
                    {
                        if (Communication.ADMIN_COMMANDS.Contains(values[0]) && !e.Client.isAdmin)
                        {
                            outPacket = new Packet(DataID.Status, e.Client.id);
                            outPacket.Add(Communication.FAILURE);
                        }
                        else if (Communication.INFO_COMMANDS.Contains(values[0]))
                        {
                            outPacket = new Packet(DataID.Info, e.Client.id);
                            outPacket.Add(ConfigurationManager.AppSettings.Get(values[0]));
                        }
                        else
                            switch (values[0])
                            {
                                //TODO handle other commands here
                                case Communication.RESTART:
                                    logger.LogInformation($@"{(e.Client.isAdmin ? "Admin" : "User")}@{e.Client.endpoint} requested server RESTART.");
                                    logger.LogInformation("Restarting server.");
                                    server.Dispose();
                                    Start();
                                    logger.LogInformation("Server RESTART successful.");
                                    break;
                                case Communication.STOP:
                                    logger.LogInformation($@"{(e.Client.isAdmin ? "Admin" : "User")}@{e.Client.endpoint} requested server STOP.");
                                    logger.LogInformation("Stopping server.");
                                    server.Dispose();
                                    outPacket = new Packet(DataID.Status, e.Client.id);
                                    outPacket.Add(Communication.SUCCESS);
                                    logger.LogInformation("Server STOP successful.");
                                    break;
                                case Communication.CONNECT:
                                    if (values[1] == Communication.START)
                                    {
                                        state = DBHandler.DBHandler.CheckUser(values[2], values[3]);
                                        outPacket = new Packet(DataID.Status, e.Client.id);
                                        outPacket.Add(state ? Communication.SUCCESS : Communication.FAILURE);
                                    }
                                    else
                                    {
                                        outPacket = new Packet(DataID.Status, e.Client.id);
                                        state = DBHandler.DBHandler.Login(values[1], values[2]);
                                        if (state)
                                            logger.LogInformation($"User@{e.Client.endpoint} logged into {values[1]} with username='{values[2]}'");
                                        outPacket.Add(state ? Communication.SUCCESS : Communication.FAILURE);
                                    }
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
