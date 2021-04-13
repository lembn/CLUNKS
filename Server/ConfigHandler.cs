using System.Configuration;

namespace Server
{
    internal static class ConfigHandler
    {
        public static void ModifyConfig(string key, string replacement)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings[key].Value = replacement;
            config.Save(ConfigurationSaveMode.Minimal);
        }
    }
}
