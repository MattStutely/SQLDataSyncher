using System.Configuration;

namespace Logger.Config
{
    public class DbConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("name", IsRequired = true)]
        public string Name
        {
            get { return this["name"] as string; }
        }

        [ConfigurationProperty("connectionStringName", IsRequired = true)]
        public string ConnectionStringName
        {
            get { return this["connectionStringName"] as string; }
        }

        [ConfigurationProperty("storedProcedureName", IsRequired = true)]
        public string StoredProcedureName
        {
            get { return this["storedProcedureName"] as string; }
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
