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

        public Worker(ILogger<Worker> _logger) => logger = _logger;

        public override Task StartAsync(CancellationToken stoppingToken)
        {
            UriBuilder uri = new UriBuilder(Assembly.GetEntryAssembly().Location);
            string path = String.Concat(Path.GetDirectoryName(Uri.UnescapeDataString(uri.Path)), @"\");
            string cfgLoc = String.Concat(path, "App.config");
            string dataLoc = String.Concat(path, "data");
            if (!File.Exists(cfgLoc))
                ConfigHandler.InitialiseConfig(cfgLoc);
            if (!Directory.Exists(dataLoc))
            {
                Directory.CreateDirectory(dataLoc);
                ConfigHandler.ModifyConfig("dataPath", dataLoc);
            }
            DBHandler.DBHandler.connectionString = String.Format(ConfigurationManager.ConnectionStrings["default"].ConnectionString, ConfigurationManager.AppSettings.Get("dataPath"));
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
            int bufferSize = Convert.ToInt32(ConfigurationManager.AppSettings.Get("bufferSize"));
            IPAddress ip = IPAddress.Parse(ConfigurationManager.AppSettings.Get("ipaddress"));
            int tcp = Convert.ToInt32(ConfigurationManager.AppSettings.Get("tcpPort"));
            int udp = Convert.ToInt32(ConfigurationManager.AppSettings.Get("udpPort"));
            server = new ServerChannel(bufferSize, ip, tcp, udp);
            if (!server.stable)
            {
                logger.LogCritical("Failed to start server. Invalid IP.");
                return;
            }
            server.Dispatch += DispatchHandler;
            if (ConfigurationManager.AppSettings.Get("newExp") == "true")
            {
                try
                {
                    DBHandler.DBHandler.LoadExp();
                }
                catch (IndexOutOfRangeException)
                {
                    logger.LogError("EXP load failed. No EXP file present at dataPath.");
                }
            }
                
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
                    values = e.Packet.Get();
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
                    values = e.Packet.Get();
                    if (Communication.ADMIN_CONFIG.Contains(values[0]))
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
                    else if (Communication.ADMIN_COMMANDS.Contains(values[0]))
                    {
                        outPacket = new Packet(DataID.Status, e.Client.id);
                        if (!e.Client.isAdmin)
                            outPacket.Add(Communication.FAILURE);
                        else
                        {
                            if (values[0] == Communication.RESTART)
                            {
                                logger.LogInformation($@"{(e.Client.isAdmin ? "Admin" : "User")}@{e.Client.endpoint} requested server RESTART.");
                                logger.LogInformation("Restarting server.");
                                server.Dispose();
                                Start();
                                logger.LogInformation("Server RESTART successful.");
                            }
                            if (values[0] == Communication.STOP)
                            {
                                logger.LogInformation($@"{(e.Client.isAdmin ? "Admin" : "User")}@{e.Client.endpoint} requested server STOP.");
                                logger.LogInformation("Stopping server.");
                                server.Dispose();
                                logger.LogInformation("Server STOP successful.");
                            }
                        }                       
                    }
                    else if (Communication.USER_COMMANDS.Contains(values[0]))
                    {
                        outPacket = new Packet(DataID.Status, e.Client.id);
                        switch (values[0])
                        {
                            //TODO handle other commands here
                            case Communication.CONNECT:
                                if (values[1] == Communication.START)
                                    outPacket.Add(DBHandler.DBHandler.CheckUser(values[2], values[3]) ? $"{values[3]}{Communication.SEPARATOR}{values[2]}" : Communication.FAILURE);
                                else
                                {
                                    state = DBHandler.DBHandler.Login(values[1], values[2]);
                                    if (state)
                                    {
                                        DBHandler.DBHandler.SetPresent(values[3], values[1]);
                                        logger.LogInformation($"User@{e.Client.endpoint} logged into '{values[3]}' with username='{values[1]}'");
                                    }
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
