using System.Configuration;

namespace Logger.Config
{
    public class LogConfigurationSection : ConfigurationSection
    {
        private static LogConfigurationSection _settings = ConfigurationManager.GetSection("LogConfigurationSection") as LogConfigurationSection;

        public static LogConfigurationSection Settings
        {
            get { return _settings; }
        }

        [ConfigurationProperty("applicationName", IsRequired = true)]
        public string ApplicationName
        {
            get { return this["applicationName"] as string; }
        }

        [ConfigurationProperty("fileAppenders")]
        public GenericConfigurationElementCollection<FileConfigurationElement> FileAppenders
        {
            get { return (GenericConfigurationElementCollection<FileConfigurationElement>)this["fileAppenders"]; }
        }

        [ConfigurationProperty("dbAppenders")]
        public GenericConfigurationElementCollection<DbConfigurationElement> DbAppenders
        {
            get { return (GenericConfigurationElementCollection<DbConfigurationElement>)this["dbAppenders"]; }
        }
    }
}

