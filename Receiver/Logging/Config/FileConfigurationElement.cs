using System.Configuration;

namespace Logger.Config
{
    public class FileConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("name", IsRequired = true)]
        public string Name
        {
            get { return this["name"] as string; }
        }

        [ConfigurationProperty("path")]
        public string Path
        {
            get { return this["path"] as string; }
        }

        [ConfigurationProperty("filename")]
        public string FileName
        {
            get { return this["filename"] as string; }
        }

        [ConfigurationProperty("minLevel")]
        public string MinLevel
        {
            get { return this["minLevel"] as string; }
        }

        [ConfigurationProperty("maxLevel")]
        public string MaxLevel
        {
            get { return this["maxLevel"] as string; }
        }
    }
}
