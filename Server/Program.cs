using Common.Channels;
using Common.Packets;
using Common.Helpers;
using System;
using System.Net;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace Server
{
    //TODO: [Server.Program] write summaries
    class Program
    {
        static ServerChannel server;

        static void Main(string[] args)
        {
            string cfgLoc = String.Concat(Assembly.GetEntryAssembly().Location, ".config");
            if (!File.Exists(cfgLoc))
                InitialiseConfig(cfgLoc);

            Start();
        }

        private static void Start()
        {
            int bufferSize = Convert.ToInt32(ConfigurationManager.AppSettings.Get("buffferSize"));
            IPAddress ip = IPAddress.Parse(ConfigurationManager.AppSettings.Get("ipaddress"));
            int tcp = Convert.ToInt32(ConfigurationManager.AppSettings.Get("tcpPort"));
            int udp = Convert.ToInt32(ConfigurationManager.AppSettings.Get("udpPort"));
            server = new ServerChannel(bufferSize, ip, tcp, udp);
            server.Dispatch += DispatchHandler;
            //TODO: [Server.Program] Check to see if a new exp needs to be loaded by checking App.config.newExp
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
                                ModifyConfig(key, value);
                            }
                            else if (command == Communication.PASSWORD)
                            {
                                string key = e.Packet.body.GetValue(String.Format(Packet.DATA, 0)).ToString();
                                string value = e.Packet.body.GetValue(String.Format(Packet.DATA, 1)).ToString();
                                ModifyConfig(key, BCrypt.Net.BCrypt.HashPassword(value));
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
                                //TODO [Server.Program] handle other commands here
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
                    //TODO [Server.Program] use DB to figure out which users need to be sent the AV packet
                    break;
            }                
        }
        
        private static void InitialiseConfig(string location)
        {
            string dataPath = String.Concat(Directory.GetCurrentDirectory(), @"\data");
            if (!Directory.Exists(dataPath))
                Directory.CreateDirectory(dataPath);

            XElement configuration = new XElement("configuration",
                new XElement("appSettings", 
                    new XElement("add", new XAttribute("key", "bufferSize"), new XAttribute("value", "1024")),
                    new XElement("add", new XAttribute("key", "username"), new XAttribute("value", "admin")),
                    new XElement("add", new XAttribute("key", "password"), new XAttribute("value", BCrypt.Net.BCrypt.HashPassword("Clunks77"))),
                    new XElement("add", new XAttribute("key", "ipaddress"), new XAttribute("value", "127.0.0.1")),
                    new XElement("add", new XAttribute("key", "tcpPort"), new XAttribute("value", "40000")),
                    new XElement("add", new XAttribute("key", "udpPort"), new XAttribute("value", "30000")),
                    new XElement("add", new XAttribute("key", "dataPath"), new XAttribute("value", dataPath)),
                    new XElement("add", new XAttribute("key", "newExp"), new XAttribute("value", "false"))));

            configuration.Save(location);
        }

        private static void ModifyConfig(string key, string replacement)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings[key].Value = replacement;
            config.Save(ConfigurationSaveMode.Modified);
        }
    }
}