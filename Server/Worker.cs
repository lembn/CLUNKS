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
            string dataLoc = String.Concat(Path.GetDirectoryName(Uri.UnescapeDataString(uri.Path)), @"\data");
            if (!Directory.Exists(dataLoc))
                Directory.CreateDirectory(dataLoc);
            if (dataLoc != ConfigurationManager.AppSettings.Get("dataPath"))
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings["dataPath"].Value = dataLoc;
                config.Save(ConfigurationSaveMode.Minimal);
            }
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
        /// The event handler invoked when the ServerChannel.Dispatch event is raised
        /// </summary>
        /// <param name="sender">The caller of the event</param>
        /// <param name="e">The event args</param>
        private static void DispatchHandler(object sender, PacketEventArgs e)
        {
            Packet outPacket = null;
            string[] values;
            bool state;
            if (e.packet.dataID == DataID.Command)
            {
                values = e.packet.Get();
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
                                    string[] currentTrace = DBHandler.DBHandler.Trace(values[5]).Split(" - "); 
                                    DBHandler.DBHandler.SetPresent(targetTrace[0], values[3], targetTrace[0] == currentTrace[0]);
                                    e.client.data["toUnset"] = String.Join(" - ", currentTrace.Where(entity => !targetTrace.Contains(entity)));
                                    if (targetTrace.Contains(values[5]))
                                        targetTrace = targetTrace.SkipWhile(entity => entity != values[5]).Skip(1).ToArray();
                                    e.client.data["requiresPassword"] = String.Join(" - ", targetTrace.Where(entity => !currentTrace.Contains(entity)));
                                    e.client.data["makePresent"] = String.Join(" - ", targetTrace);
                                    e.client.data["ETTarget"] = values[2];
                                    e.client.data["ETTargetSubserver"] = targetTrace[0];
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
                                        e.client.data.Remove("makePresent");
                                        foreach (string entity in e.client.data["requiresPassword"].ToString().Split(" - ").Where(x => !String.IsNullOrEmpty(x)))
                                        {
                                            DBHandler.DBHandler.SetPresent(entity, values[3]);
                                            logger.LogInformation($"User@{e.client.endpoint} ({values[3]}) logged into of '{entity}'");
                                        }
                                        e.client.data.Remove("requiresPassword");
                                        e.client.data.Remove("ETTarget");
                                        foreach (string entity in e.client.data["toUnset"].ToString().Split(" - ").Where(x => !String.IsNullOrEmpty(x)))
                                        {
                                            DBHandler.DBHandler.SetPresent(entity, values[3], false);
                                            logger.LogInformation($"User@{e.client.endpoint} ({values[3]}) logged out of '{entity}'");                                               
                                        }
                                        e.client.data.Remove("toUnset");
                                        DBHandler.DBHandler.SetPresent(e.client.data["ETTargetSubserver"].ToString(), values[3]);
                                        e.client.data.Remove("ETTargetSubserver");
                                        DBHandler.DBHandler.SetPresent(values[2], values[3]);
                                        logger.LogInformation($"User@{e.client.endpoint} logged into '{values[2]}' with username='{values[3]}'");
                                        outPacket.Add(Communication.SUCCESS, DBHandler.DBHandler.Trace(values[2]));
                                    }
                                    else
                                        outPacket.Add(next, Communication.INCOMPLETE);
                                }
                                else
                                    outPacket.Add(Communication.FAILURE);
                            }
                            else
                            {
                                string[] trace = values[5].Split(" - ").Reverse().ToArray();
                                int i;
                                for (i = trace.Length - 1; i >= 0; i--)
                                {
                                    if (trace[i] == values[2])
                                        break;
                                    DBHandler.DBHandler.SetPresent(trace[i], values[3], false);
                                    logger.LogInformation($"User@{e.client.endpoint} ({values[3]}) logged out of '{trace[i]}'");
                                }
                                outPacket.Add(Communication.SUCCESS, String.Join(" - ", trace.Take(i + 1)));
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
                                string pwd = (string)e.client.data[currentEntity];
                                e.client.data.Remove(currentEntity);
                                trace.RemoveAt(i);
                                if (!String.IsNullOrEmpty(pwd))
                                {
                                    state = BCrypt.Net.BCrypt.Verify(values[1], pwd);
                                    broke = true;
                                    if (i > 0)
                                        for (int x = 1; i - x < 1; x++)
                                        {
                                            next = trace[i - x];
                                            if (!String.IsNullOrEmpty((string)e.client.data[next]))
                                                break;
                                        }                                   
                                    break;
                                }                                        
                            }
                            if (state)
                            {
                                if (broke && trace.Count > 0)
                                {
                                    e.client.data["requiresPassword"] = String.Join(" - ", trace);
                                    outPacket.Add(next, Communication.INCOMPLETE);
                                }
                                else
                                {
                                    e.client.data.Remove("requiresPassword");
                                    foreach (string entity in e.client.data["makePresent"].ToString().Split(" - ").Where(x => !String.IsNullOrEmpty(x)))
                                    {
                                        DBHandler.DBHandler.SetPresent(entity, values[2]);
                                        logger.LogInformation($"User@{e.client.endpoint} ({values[2]}) logged into of '{entity}'");
                                    }
                                    e.client.data.Remove("makePresent");
                                    foreach (string entity in e.client.data["toUnset"].ToString().Split(" - ").Where(x => !String.IsNullOrEmpty(x)))
                                    {
                                        DBHandler.DBHandler.SetPresent(entity, values[2], false);
                                        logger.LogInformation($"User@{e.client.endpoint} ({values[2]}) logged out of '{entity}'");
                                    }
                                    e.client.data.Remove("toUnset");
                                    DBHandler.DBHandler.SetPresent(e.client.data["ETTargetSubserver"].ToString(), values[2]);
                                    e.client.data.Remove("ETTargetSubserver");
                                    outPacket.Add(Communication.SUCCESS, DBHandler.DBHandler.Trace(e.client.data["ETTarget"].ToString()));
                                    e.client.data.Remove("ETTarget");
                                }
                            }
                            else
                            {
                                e.client.data.Remove("presentChanged");
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
                            e.client.data["DB_userID"] = DBHandler.DBHandler.GetUserID(e.client.data["username"].ToString());
                            e.client.data.Remove("username");
                        }
                        break;
                }    
                server.Add(outPacket, e.client);                
            }
            else if (e.packet.dataID == DataID.AV)
            {
                //TODO: use DB to figure out which users need to be sent the AV packet
                throw new NotImplementedException();
            }
        }

        public static void FailHandler(object sender, ChannelFailEventArgs e) => logger.LogError(e.Message);

        /// <summary>
        /// The event handler invoked when ServerChannel.RemoveClientEvent event is called
        /// </summary>
        /// <param name="sender">The caller of the event</param>
        /// <param name="e">The event args</param>
        public static void RemoveClientHandler(object sender, RemoveClientEventArgs e)
        {
            string username = DBHandler.DBHandler.Logout(e.ID);
            logger.LogInformation($"User@{e.Client.endpoint} ({username}) logged out of CLUNKS'");
        }
    }
}
