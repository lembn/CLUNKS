using System;
using System.Configuration;
using System.IO;
using System.Xml.Linq;

namespace Server
{
    internal static class ConfigHandler
    {
        internal static void InitialiseConfig(string location)
        {
            XElement configuration = new XElement("configuration",
                new XElement("connectionStrings",
                    new XElement("add", new XAttribute("name", "default"), new XAttribute("connectionString", @"Data Source=$\data.db;Cache=Shared")),
                new XElement("appSettings",
                    new XElement("add", new XAttribute("key", "bufferSize"), new XAttribute("value", "1024")),
                    new XElement("add", new XAttribute("key", "username"), new XAttribute("value", "admin")),
                    new XElement("add", new XAttribute("key", "password"), new XAttribute("value", BCrypt.Net.BCrypt.HashPassword("Clunks77"))),
                    new XElement("add", new XAttribute("key", "ipaddress"), new XAttribute("value", "127.0.0.1")),
                    new XElement("add", new XAttribute("key", "tcpPort"), new XAttribute("value", "40000")),
                    new XElement("add", new XAttribute("key", "udpPort"), new XAttribute("value", "30000")),
                    new XElement("add", new XAttribute("key", "dataPath"), new XAttribute("value", String.Concat(Directory.GetCurrentDirectory(), @"\data"))),
                    new XElement("add", new XAttribute("key", "newExp"), new XAttribute("value", "false")))));

            configuration.Save(location);
        }

        internal static void ModifyConfig(string key, string replacement)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings[key].Value = replacement;
            config.Save(ConfigurationSaveMode.Minimal);
        }
    }
}
