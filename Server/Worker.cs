using Common.Channels;
using Common.Helpers;
using Common.Packets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    internal class Worker : BackgroundService
    {
        private static ILogger<Worker> logger; //Logger object for logging information
        private static ServerChannel server; //Server channel for network communication

        public Worker(ILogger<Worker> _logger) => logger = _logger;

        /// <summary>
        /// A method called at the start of the WorkerService's lifetime used to setup the required resources used at runtime 
        /// </summary>
        /// <param name="stoppingToken">A CancellationToken to observe</param>
        /// <returns>A completed Task</returns>
        public override Task StartAsync(CancellationToken stoppingToken)
        {
            UriBuilder uri = new UriBuilder(Assembly.GetEntryAssembly().Location);
            string path = String.Concat(Path.GetDirectoryName(Uri.UnescapeDataString(uri.Path)), @"\");
            string cfgLoc = String.Concat(path, "App.config");
            string dataLoc = String.Concat(path, "data");
            if (!File.Exists(cfgLoc))
                ConfigHandler.InitialiseConfig(cfgLoc);
            if (!Directory.Exists(dataLoc))
                Directory.CreateDirectory(dataLoc);
            var a = ConfigurationManager.AppSettings.Get("dataPath");
            if (dataLoc != ConfigurationManager.AppSettings.Get("dataPath"))
                ConfigHandler.ModifyConfig("dataPath", dataLoc);
            DBHandler.DBHandler.connectionString = String.Format(ConfigurationManager.ConnectionStrings["default"].ConnectionString, ConfigurationManager.AppSettings.Get("dataPath"));
            Start();
            return Task.CompletedTask;
        }

        /// <summary>
        /// A method called at the end of the WorkerService's lifetime used to cleanup any open handles
        /// </summary>
        /// <param name="cancellationToken">A CancellationToken to observe</param>
        /// <returns>A completed Task</returns>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            server.Dispose();
            return Task.CompletedTask;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;

        /// <summary>
        /// A method to intialise the Server (setup ServerChannel and load EXPs into the database if needed)
        /// </summary>
        private static void Start()
        {
            int bufferSize = Convert.ToInt32(ConfigurationManager.AppSettings.Get("bufferSize"));
            IPAddress ip = IPAddress.Parse(ConfigurationManager.AppSettings.Get("ipaddress"));
            int tcp = Convert.ToInt32(ConfigurationManager.AppSettings.Get("tcpPort"));
            int udp = Convert.ToInt32(ConfigurationManager.AppSettings.Get("udpPort"));
            bool state = true;
            server = new ServerChannel(bufferSize, ip, tcp, udp, ref state);
            if (!state)
            {
                logger.LogCritical("Failed to start server. Invalid IP.");
                return;
            }
            server.ChannelFail += new Channel.ChannelFailEventHanlder(FailHandler);
            server.Dispatch += new Channel.DispatchEventHandler(DispatchHandler);
            server.RemoveClientEvent += new ServerChannel.RemoveClientEventHandler(RemoveClientHandler);
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

        /// <summary>
        /// The event handler called when the ServerChannel.Dispatch event is raised
        /// </summary>
        /// <param name="sender">The caller of the event</param>
        /// <param name="e">The event args</param>
        private static void DispatchHandler(object sender, PacketEventArgs e)
        {
            Packet outPacket = null;
            string[] values;
            bool state;
            switch (e.packet.dataID)
            {
                case DataID.Login:
                    values = e.packet.Get();
                    if (values[0] == ConfigurationManager.AppSettings.Get("username"))
                        e.client.isAdmin = BCrypt.Net.BCrypt.Verify(values[1], ConfigurationManager.AppSettings.Get("password"));
                    else
                        e.client.isAdmin = false;
                    outPacket = new Packet(DataID.Status, e.client.id);
                    outPacket.Add(e.client.isAdmin ? Communication.SUCCESS : Communication.FAILURE);
                    server.Add(outPacket, e.client);
                    logger.LogInformation($"'{e.client.endpoint}' logged in as admin.");
                    break;
                case DataID.Command:
                    values = e.packet.Get();
                    if (Communication.ADMIN_CONFIG.Contains(values[0]))
                    {
                        outPacket = new Packet(DataID.Status, e.client.id);
                        outPacket.Add(e.client.isAdmin ? Communication.SUCCESS : Communication.FAILURE);
                        if (e.client.isAdmin)
                        {
                            if (Communication.ADMIN_CONFIG.Contains(values[0]))
                            {
                                logger.LogInformation($"Admin@{e.client.endpoint} changed '{values[1]}' from '{ConfigurationManager.AppSettings.Get(values[1])}' to '{values[2]}' in config.");
                                ConfigHandler.ModifyConfig(values[1], values[2]);
                            }
                            else if (values[0] == Communication.PASSWORD)
                            {
                                ConfigHandler.ModifyConfig(values[1], BCrypt.Net.BCrypt.HashPassword(values[2]));
                                logger.LogInformation($"Admin@{e.client.endpoint} changed password.");
                            }
                        }
                    }
                    else if (Communication.ADMIN_COMMANDS.Contains(values[0]))
                    {
                        outPacket = new Packet(DataID.Status, e.client.id);
                        if (!e.client.isAdmin)
                            outPacket.Add(Communication.FAILURE);
                        else
                        {
                            if (values[0] == Communication.RESTART)
                            {
                                logger.LogInformation($@"{(e.client.isAdmin ? "Admin" : "User")}@{e.client.endpoint} requested server RESTART.");
                                logger.LogInformation("Restarting server.");
                                server.Dispose();
                                Start();
                                logger.LogInformation("Server RESTART successful.");
                            }
                            if (values[0] == Communication.STOP)
                            {
                                logger.LogInformation($@"{(e.client.isAdmin ? "Admin" : "User")}@{e.client.endpoint} requested server STOP.");
                                logger.LogInformation("Stopping server.");
                                server.Dispose();
                                logger.LogInformation("Server STOP successful.");
                            }
                        }                       
                    }
                    else if (Communication.USER_COMMANDS.Contains(values[0]))
                    {
                        outPacket = new Packet(DataID.Status, e.client.id);
                        switch (values[0])
                        {
                            case Communication.CONNECT:
                                if (values[1] == Communication.START)
                                {
                                    if (values[4] == Communication.FORWARD)
                                    {
                                        state = DBHandler.DBHandler.UserInEntity(values[2], values[3]);
                                        if (state)
                                        {
                                            string next = String.Empty;
                                            string[] targetTrace = DBHandler.DBHandler.Trace(values[2]).Split(" - ");
                                            if (String.IsNullOrEmpty(values[5]))
                                                values[5] = targetTrace[0];
                                            targetTrace = targetTrace.SkipWhile(entity => entity != values[5]).Skip(1).ToArray();
                                            string[] currentTrace = DBHandler.DBHandler.Trace(values[5]).Split(" - "); 
                                            e.client.data["requiresPassword"] = String.Join(" - ", targetTrace);
                                            e.client.data["ETTarget"] = values[2];
                                            e.client.data["toUnset"] = String.Join(" - ", currentTrace.Where(entity => !targetTrace.Contains(entity)));
                                            foreach (string entity in targetTrace.Reverse())
                                            {
                                                string pwd = DBHandler.DBHandler.GetEntityPassword(entity);
                                                e.client.data[entity] = pwd;
                                                if (!String.IsNullOrEmpty(pwd))
                                                {
                                                    state = false;
                                                    if (String.IsNullOrEmpty(next))
                                                        next = entity;
                                                }
                                            }
                                            if (state)
                                            {
                                                e.client.data.Remove("requiresPassword");
                                                string[] toUnset = e.client.data["ETTarget"].ToString().Split(" - ");
                                                foreach (string entity in toUnset)
                                                {
                                                    DBHandler.DBHandler.SetPresent(entity, values[3], false);
                                                    logger.LogInformation($"User@{e.client.endpoint} ({values[3]}) logged out of '{entity}'");
                                                }
                                                e.client.data.Remove("ETTarget");
                                                e.client.data.Remove("toUnset");
                                                DBHandler.DBHandler.SetPresent(values[2], values[3]);
                                                logger.LogInformation($"User@{e.client.endpoint} logged into '{values[2]}' with username='{values[3]}'");
                                                outPacket.Add(Communication.SUCCESS, values[2], values[3]);
                                            }
                                            else
                                                outPacket.Add(next, Communication.INCOMPLETE);
                                        }
                                        else
                                            outPacket.Add(Communication.FAILURE);
                                    }
                                    else
                                    {
                                        string[] trace = values[5].Split(" - ");
                                        int i;
                                        for (i = trace.Length - 1; i >= 0; i--)
                                        {
                                            if (trace[i] == values[2])
                                                break;
                                            DBHandler.DBHandler.SetPresent(trace[i], values[3], false);
                                            logger.LogInformation($"User@{e.client.endpoint} ({values[3]}) logged out of '{trace[i]}'");
                                        }
                                        outPacket.Add(Communication.SUCCESS, String.Join(" - ", trace.Take(trace.Length - i)));
                                    }
                                }
                                else
                                {
                                    state = true;
                                    List<string> trace = e.client.data["requiresPassword"].ToString().Split(" - ").ToList();
                                    bool broke = false;
                                    string currentEntity = String.Empty;
                                    string next = String.Empty;
                                    for (int i = trace.Count - 1; i >= 0; i--)
                                    {
                                        currentEntity = trace[i];
                                        if (i > 0)
                                            next = trace[i - 1];
                                        string pwd = (string)e.client.data[currentEntity];
                                        e.client.data.Remove(currentEntity);
                                        trace.RemoveAt(i);
                                        if (!String.IsNullOrEmpty(pwd))
                                        {
                                            state = BCrypt.Net.BCrypt.Verify(values[1], pwd);
                                            broke = true;
                                            break;
                                        }                                        
                                    }
                                    if (state)
                                    {
                                        DBHandler.DBHandler.SetPresent(currentEntity, values[2]);
                                        logger.LogInformation($"User@{e.client.endpoint} logged into '{currentEntity}' with username='{values[2]}'");
                                        if (broke && trace.Count > 0)
                                        {
                                            e.client.data["requiresPassword"] = String.Join(" - ", trace);
                                            outPacket.Add(next, Communication.INCOMPLETE);
                                        }
                                        else
                                        {
                                            e.client.data.Remove("requiresPassword");
                                            string[] toUnset = e.client.data["ETTarget"].ToString().Split(" - ");
                                            foreach (string entity in toUnset)
                                            {
                                                DBHandler.DBHandler.SetPresent(entity, values[3], false);
                                                logger.LogInformation($"User@{e.client.endpoint} ({values[3]}) logged out of '{entity}'");
                                            }
                                            e.client.data.Remove("toUnset");
                                            outPacket.Add(Communication.SUCCESS, DBHandler.DBHandler.Trace(e.client.data["ETTarget"].ToString()));
                                            e.client.data.Remove("ETTarget");
                                        }
                                    }
                                    else
                                    {
                                        e.client.data.Remove("requiresPassword");
                                        e.client.data.Remove("ETTarget");
                                        e.client.data.Remove("toUnset");
                                        foreach (string entity in trace)
                                            if (e.client.data.ContainsKey(entity))
                                                e.client.data.Remove(entity);
                                        outPacket.Add(Communication.FAILURE);                                
                                    }
                                }
                                break;
                            case Communication.DISCONNECT:
                                state = DBHandler.DBHandler.UserInEntity(values[1], values[2]);
                                if (state)
                                {
                                    DBHandler.DBHandler.SetPresent(values[1], values[2], false);
                                    logger.LogInformation($"User@{e.client.endpoint} ({values[2]}) logged out of '{values[1]}'");
                                }
                                outPacket.Add(state ? Communication.SUCCESS : Communication.FAILURE);
                                break;
                            case Communication.LOGIN:
                                if (values[1] == Communication.START)
                                {
                                    state = DBHandler.DBHandler.UserExists(values[2]);
                                    e.client.data["username"] = values[2];
                                    outPacket.Add(state ? Communication.INCOMPLETE : Communication.FAILURE);
                                }
                                else
                                {
                                    state = DBHandler.DBHandler.LoginUser(e.client.data["username"].ToString(), values[1]);
                                    if (state)
                                        logger.LogInformation($"User@{e.client.endpoint} logged in as '{e.client.data["username"]}'");
                                    outPacket.Add(state ? Communication.SUCCESS : Communication.FAILURE, e.client.data["username"].ToString());
                                    e.client.data.Remove("username");
                                    e.client.data["DB_userID"] = DBHandler.DBHandler.GetUserID(e.client.data["username"].ToString());
                                }
                                break;
                        }                     
                    }
                    server.Add(outPacket, e.client);
                    break;
                case DataID.AV:
                    //TODO: use DB to figure out which users need to be sent the AV packet
                    break;
            }
        }

        public static void FailHandler(object sender, ChannelFailEventArgs e) => logger.LogError(e.Message);

        public static void RemoveClientHandler(object sender, RemoveClientEventArgs e) => DBHandler.DBHandler.Logout(e.ID);
    }
}
